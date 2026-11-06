// BlockTower VR multiplayer server: rooms (4-digit codes), the random queue, seats, turns and relaying. It runs no
// physics: the player whose turn it is simulates the tower on their Quest and sends the blocks; everyone sends their
// head and hands. LAN only (docs/superpowers/specs/2026-11-06-quest-multiplayer-design.md).
// Messages are JSON text frames {t: type, ...}; see the handlers below for each. Arrays are flat (Unity's JsonUtility
// reads no nested ones): blocks `s` are 9 numbers a block (index, position xyz, rotation xyzw, 1 if off the tower).
// Absent values are -1 / "" rather than null, for the same reader.
'use strict';
const { WebSocketServer } = require('ws');

const SEATS = 4;
const RANDOM_WAIT = 10;     // s: a random room starts this long after its second player, or at once when full
const ROUND_DELAY = 10;     // s: tower down to the next round
const TURN_LIMIT = 60;      // s: a turn not done by then passes on
const STRIDE = 9;           // numbers a block in `s`

function makeServer(port, log = () => {}, { randomWait = RANDOM_WAIT, roundDelay = ROUND_DELAY, turnLimit = TURN_LIMIT } = {}) {
  const wss = new WebSocketServer({ port });
  const rooms = new Map();  // code -> room (random rooms too, under a code no one is shown)
  let nextId = 1;

  const send = (c, msg) => { if (c.ws.readyState === 1) c.ws.send(JSON.stringify(msg)); };
  const members = room => room.seats.filter(Boolean);
  const toRoom = (room, msg, except) => { for (const c of members(room)) if (c !== except) send(c, msg); };
  const lobby = () => {
    for (const c of wss.clients) if (c.player) send(c.player, { t: 'lobby', online: wss.clients.size });
  };

  function newCode() {
    for (;;) {
      const code = String(Math.floor(Math.random() * 10000)).padStart(4, '0');
      if (!rooms.has(code)) return code;
    }
  }

  function roomState(room) {
    for (const c of members(room))
      send(c, {
        t: 'room', code: room.random ? '' : room.code, random: room.random, seat: c.seat, host: room.host.seat,
        members: members(room).map(m => ({ seat: m.seat })),
        countdown: room.startAt ? Math.max(0, Math.ceil((room.startAt - Date.now()) / 1000)) : -1,
      });
  }

  function enter(c, room) {
    const seat = room.seats.indexOf(null);
    room.seats[seat] = c;
    c.seat = seat;
    c.room = room;
    log(`${c.name} -> room ${room.code} seat ${seat}`);
    if (room.random && members(room).length === 2 && !room.startAt) {
      room.startAt = Date.now() + randomWait * 1000;
      room.timer = setTimeout(() => begin(room), randomWait * 1000);
    }
    if (room.random && members(room).length === SEATS) begin(room);
    else roomState(room);
    lobby();
  }

  function begin(room) {
    if (room.started || members(room).length < 2) return;
    clearTimeout(room.timer);
    room.started = true;
    room.seed = Math.floor(Math.random() * 1e9);
    room.blocks = new Map();
    log(`room ${room.code} starts with ${members(room).length}`);
    toRoom(room, { t: 'start', seed: room.seed, turn: room.host.seat, limit: turnLimit, members: members(room).map(m => ({ seat: m.seat, name: m.name })) });
    setTurn(room, room.host.seat);
    lobby();
  }

  /// The next seated player after `seat`, round the table
  function after(room, seat) {
    for (let i = 1; i <= SEATS; i++) if (room.seats[(seat + i) % SEATS]) return (seat + i) % SEATS;
    return seat;
  }

  /// The turn to `seat`, its clock started; out of time, it passes on with the tower as last sent
  function setTurn(room, seat) {
    room.turn = seat;
    clearTimeout(room.turnTimer);
    room.turnTimer = setTimeout(() => {
      log(`room ${room.code}: seat ${room.turn} out of time`);
      nextTurn(room, false, true);
    }, turnLimit * 1000);
  }

  const flat = blocks => [...blocks.values()].flat();

  /// The turn to the next seated player; `took`: the last one took a block (their score), `late`: their time ran out
  function nextTurn(room, took, late = false) {
    const from = room.turn;
    setTurn(room, after(room, room.turn));
    toRoom(room, { t: 'turn', seat: room.turn, from, took, late, limit: turnLimit, s: flat(room.blocks) });
  }

  function leave(c, why) {
    const room = c.room;
    if (!room) return;
    room.seats[c.seat] = null;
    c.room = null;
    log(`${c.name} left room ${room.code} (${why})`);
    const left = members(room);
    if (left.length === 0) {
      clearTimeout(room.timer);
      clearTimeout(room.roundTimer);
      clearTimeout(room.turnTimer);
      rooms.delete(room.code);
    } else if (room.started && left.length < 2) {
      clearTimeout(room.roundTimer);
      clearTimeout(room.turnTimer);
      for (const m of left) { send(m, { t: 'end', msg: '다른 사람이 모두 나갔습니다' }); m.room = null; }
      rooms.delete(room.code);
    } else {
      if (room.host === c) room.host = left[0];
      if (room.random && !room.started && left.length < 2) { clearTimeout(room.timer); room.startAt = null; }
      toRoom(room, { t: 'left', seat: c.seat });
      if (room.started && room.turn === c.seat && !room.over) nextTurn(room, false);
      else roomState(room);
    }
    lobby();
  }

  const handlers = {
    hello(c, m) { c.name = String(m.name || `Player ${c.id}`).slice(0, 24); lobby(); },
    create(c) {
      leave(c, 'new room');
      const code = newCode();
      const room = { code, random: false, seats: Array(SEATS).fill(null), host: c, started: false };
      rooms.set(code, room);
      enter(c, room);
    },
    join(c, m) {
      const room = rooms.get(String(m.code));
      if (!room || room.random) return send(c, { t: 'error', msg: '없는 방입니다' });
      if (room.started) return send(c, { t: 'error', msg: '이미 시작한 방입니다' });
      if (!room.seats.includes(null)) return send(c, { t: 'error', msg: '방이 꽉 찼습니다' });
      leave(c, 'joining another');
      enter(c, room);
    },
    random(c) {
      leave(c, 'random');
      let room = [...rooms.values()].find(r => r.random && !r.started && r.seats.includes(null));
      if (!room) {
        room = { code: 'R' + newCode(), random: true, seats: Array(SEATS).fill(null), host: c, started: false };
        rooms.set(room.code, room);
      }
      enter(c, room);
    },
    leave(c) { leave(c, 'left'); },
    start(c) {
      const room = c.room;
      if (!room || room.random || room.host !== c) return;
      if (members(room).length < 2) return send(c, { t: 'error', msg: '2명 이상이어야 시작할 수 있습니다' });
      begin(room);
    },
    pose(c, m) { if (c.room) toRoom(c.room, { t: 'pose', seat: c.seat, h: m.h, l: m.l, r: m.r }, c); },
    // the tower, from the one who simulates it: kept for whoever takes the turn next
    blocks(c, m) {
      const room = c.room;
      if (!room || !room.started || room.turn !== c.seat) return;
      const s = Array.isArray(m.s) ? m.s : [];
      if (m.full) room.blocks.clear();
      for (let i = 0; i + STRIDE <= s.length; i += STRIDE) room.blocks.set(s[i], s.slice(i, i + STRIDE));
      toRoom(room, { t: 'blocks', seat: c.seat, s, full: !!m.full }, c);
    },
    taken(c) {
      const room = c.room;
      if (room && room.started && !room.over && room.turn === c.seat) nextTurn(room, true);
    },
    fell(c) {
      const room = c.room;
      if (!room || !room.started || room.over || room.turn !== c.seat) return;
      room.over = true;
      clearTimeout(room.turnTimer);
      toRoom(room, { t: 'over', loser: c.seat, countdown: roundDelay });
      room.roundTimer = setTimeout(() => {
        room.over = false;
        room.seed = Math.floor(Math.random() * 1e9);
        room.blocks.clear();
        const first = room.seats[after(room, c.seat)] ? after(room, c.seat) : room.host.seat;  // the loser's neighbour starts
        toRoom(room, { t: 'round', seed: room.seed, turn: first, limit: turnLimit });
        setTurn(room, first);
      }, roundDelay * 1000);
    },
  };

  wss.on('connection', ws => {
    const c = { ws, id: nextId++, name: null, room: null, seat: -1, alive: true };
    ws.player = c;
    ws.on('pong', () => { c.alive = true; });
    ws.on('message', data => {
      let m;
      try { m = JSON.parse(data); } catch { return; }
      const h = handlers[m.t];
      if (h) h(c, m);
    });
    ws.on('close', () => { leave(c, 'disconnected'); lobby(); });
    lobby();
  });

  // a Quest taken off goes quiet without closing: drop it after a missed ping
  const beat = setInterval(() => {
    for (const ws of wss.clients) {
      if (!ws.player.alive) { ws.terminate(); continue; }
      ws.player.alive = false;
      ws.ping();
    }
  }, 5000);
  wss.on('close', () => clearInterval(beat));
  return wss;
}

module.exports = { makeServer };

if (require.main === module) {
  const port = Number(process.env.PORT || 3010);
  makeServer(port, s => console.log(new Date().toISOString(), s));
  console.log(`BlockTower VR server on :${port}`);
}
