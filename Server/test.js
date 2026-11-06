// node test.js — plays rooms, random matching, turns, relaying, a fall and leaving against a local server
'use strict';
const assert = require('assert');
const WebSocket = require('ws');
const { makeServer } = require('./server');

const PORT = 3999;
const wss = makeServer(PORT, () => {}, { randomWait: 0.3, roundDelay: 0.2, turnLimit: 0.5 });

function client(name) {
  const ws = new WebSocket(`ws://127.0.0.1:${PORT}`);
  const inbox = [];
  const waiters = [];
  ws.on('message', d => {
    const m = JSON.parse(d);
    const w = waiters.findIndex(x => x.t === m.t);
    if (w >= 0) waiters.splice(w, 1)[0].resolve(m); else inbox.push(m);
  });
  const c = {
    send: m => ws.send(JSON.stringify(m)),
    // the next message of type t (one already in, or the next to come)
    next: t => {
      const i = inbox.findIndex(m => m.t === t);
      if (i >= 0) return Promise.resolve(inbox.splice(i, 1)[0]);
      return new Promise((resolve, reject) => {
        waiters.push({ t, resolve });
        setTimeout(() => reject(new Error(`${name}: no ${t}`)), 2000);
      });
    },
    drain: t => { for (let i = inbox.length - 1; i >= 0; i--) if (inbox[i].t === t) inbox.splice(i, 1); },
    close: () => ws.close(),
    open: new Promise(r => ws.on('open', r)),
  };
  c.open.then(() => c.send({ t: 'hello', name }));
  return c;
}

(async () => {
  // friends: a room by code
  const a = client('A'), b = client('B'), c = client('C');
  await Promise.all([a.open, b.open, c.open]);
  a.send({ t: 'create' });
  const room = await a.next('room');
  assert.match(room.code, /^\d{4}$/);
  assert.equal(room.seat, 0);
  c.send({ t: 'join', code: '----' });
  assert.equal((await c.next('error')).msg, '없는 방입니다');
  a.send({ t: 'start' });
  assert.equal((await a.next('error')).msg, '2명 이상이어야 시작할 수 있습니다');
  b.send({ t: 'join', code: room.code });
  const bRoom = await b.next('room');
  assert.equal(bRoom.seat, 1);
  assert.equal(bRoom.members.length, 2);
  b.send({ t: 'start' });  // not the host: nothing
  a.send({ t: 'start' });
  const start = await b.next('start');
  assert.equal(start.turn, 0);
  await a.next('start');
  c.send({ t: 'join', code: room.code });
  assert.equal((await c.next('error')).msg, '이미 시작한 방입니다');

  // relaying: poses to the others, blocks only from the turn holder
  a.send({ t: 'pose', h: [1, 2, 3, 0, 0, 0, 1], l: null, r: null });
  assert.deepEqual((await b.next('pose')).h, [1, 2, 3, 0, 0, 0, 1]);
  b.send({ t: 'blocks', full: true, s: [0, 9, 9, 9, 0, 0, 0, 1, 0] });  // not B's turn: dropped
  a.send({ t: 'blocks', full: true, s: [0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0, 1, 0, 0, 0, 0, 1, 0] });
  assert.equal((await b.next('blocks')).s.length, 18);
  a.send({ t: 'blocks', s: [1, 0, 2, 0, 0, 0, 0, 1, 1] });
  await b.next('blocks');
  // the turn passes with the tower as A left it
  a.send({ t: 'taken' });
  const turn = await b.next('turn');
  assert.equal(turn.seat, 1);
  assert.equal(turn.took, true);
  assert.deepEqual(turn.s, [0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0, 2, 0, 0, 0, 0, 1, 1]);
  assert.equal((await a.next('turn')).seat, 1);  // A hears it too
  // B dawdles: after the limit the turn comes back to A, not taken
  const late = await a.next('turn');
  assert.equal(late.seat, 0);
  assert.equal(late.late, true);
  assert.equal(late.took, false);
  a.send({ t: 'taken' });
  await b.next('turn');
  b.drain('turn'); a.drain('turn');
  // B topples it: game over, then a new round from B's neighbour
  a.send({ t: 'fell' });  // not A's turn: ignored
  b.send({ t: 'fell' });
  assert.equal((await a.next('over')).loser, 1);
  const round = await a.next('round');
  assert.equal(round.turn, 0);
  // B leaves: A is alone, the game ends
  b.close();
  assert.equal((await a.next('end')).msg, '다른 사람이 모두 나갔습니다');

  // random: two wait, a third joins in the countdown, it starts
  const r1 = client('R1'), r2 = client('R2');
  await Promise.all([r1.open, r2.open]);
  r1.send({ t: 'random' });
  assert.equal((await r1.next('room')).countdown, -1);
  r2.send({ t: 'random' });
  assert.equal((await r2.next('room')).countdown, 1);
  c.send({ t: 'random' });
  await c.next('room');
  const rs = await c.next('start');
  assert.equal(rs.members.length, 3);
  // the turn holder takes the headset off: gone at once, the turn moves on
  r1.send({ t: 'leave' });
  assert.equal((await r2.next('turn')).seat, 1);
  await new Promise(r => setTimeout(r, 200));
  r2.drain('left'); c.drain('left'); c.drain('turn');
  c.send({ t: 'taken' });  // not C's turn (seat 1 is R2's): nothing
  r2.send({ t: 'taken' });
  const t2 = await c.next('turn');
  assert.deepEqual([t2.seat, t2.took], [2, true]);

  for (const x of [a, r2, c]) x.close();
  wss.close();
  console.log('server test: ALL PASS');
  process.exit(0);
})().catch(e => { console.error(e); process.exit(1); });
