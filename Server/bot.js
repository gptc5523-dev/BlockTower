// A stand-in player for testing with one Quest: node bot.js [url] random | follow | create | join CODE
// It sits wherever the server seats it, copies the Quest's head and hands turned 180 deg about the tower (so it
// shows across the table), and on its turn waits 3 s and passes without touching the tower.
'use strict';
const WebSocket = require('ws');

const args = process.argv.slice(2);
const url = args[0] && args[0].startsWith('ws') ? args.shift() : 'ws://127.0.0.1:3010';
const [mode = 'random', code] = args;
const ws = new WebSocket(url);
const send = m => ws.send(JSON.stringify(m));
let seat = -1, centre = null, people = new Set();

// the tower's axis: the middle of the blocks the Quest sends
function learnCentre(s) {
  if (centre || !s || s.length < 9) return;
  let x = 0, z = 0, n = 0;
  for (let i = 0; i + 9 <= s.length; i += 9) if (!s[i + 8]) { x += s[i + 1]; z += s[i + 3]; n++; }
  if (n) centre = [x / n, z / n];
}

// a pose array (7 a joint) moved to another place at the table about the tower axis: by seat, turned half round
// (across the tower), mirrored across x (beside) or across z (facing) — three bots, three places
function turn(a) {
  if (!centre || !a || !a.length) return a;
  const kind = seat % 3;
  const out = a.slice();
  for (let i = 0; i + 7 <= a.length; i += 7) {
    const dx = a[i] - centre[0], dz = a[i + 2] - centre[1];
    const [x, y, z, w] = a.slice(i + 3, i + 7);
    if (kind === 0) {        // half-turn about Y: q' = (0,1,0,0) * q
      out[i] = centre[0] - dx; out[i + 2] = centre[1] - dz;
      out[i + 3] = z; out[i + 4] = w; out[i + 5] = -x; out[i + 6] = -y;
    } else if (kind === 1) { // mirror x -> -x
      out[i] = centre[0] - dx;
      out[i + 3] = x; out[i + 4] = -y; out[i + 5] = -z; out[i + 6] = w;
    } else {                 // mirror z -> -z
      out[i + 2] = centre[1] - dz;
      out[i + 3] = -x; out[i + 4] = -y; out[i + 5] = z; out[i + 6] = w;
    }
  }
  return out;
}

// follow: join the random queue only once someone (the person testing) is waiting in it, so bots never start a game
// among themselves. Several follow bots fill the table with one person
let joined = false;
ws.on('open', () => {
  send({ t: 'hello', name: 'bot' });
  if (mode === 'join') send({ t: 'join', code });
  else if (mode !== 'follow') send({ t: mode });
  joined = mode !== 'follow';
  console.log('bot:', mode, code || '');
});
ws.on('message', d => {
  const m = JSON.parse(d);
  if (m.t !== 'pose' && m.t !== 'blocks') console.log('<-', JSON.stringify(m).slice(0, 160));
  if (m.t === 'lobby' && !joined && m.waiting > 0) { joined = true; send({ t: 'random' }); }
  if (m.t === 'room') { seat = m.seat; if (m.code) console.log('room code', m.code); }
  if (m.t === 'blocks') learnCentre(m.s);
  if (m.t === 'turn' || m.t === 'start') learnCentre(m.s);
  // copy only the people: bots copying bots echo without end (it flooded the Quest off the server)
  if (m.t === 'start') people = new Set(m.members.filter(x => x.name !== 'bot').map(x => x.seat));
  if (m.t === 'pose' && people.has(m.seat)) send({ t: 'pose', h: turn(m.h), l: turn(m.l), r: turn(m.r) });
  if ((m.t === 'turn' && m.seat === seat) || ((m.t === 'start' || m.t === 'round') && m.turn === seat))
    setTimeout(() => send({ t: 'taken' }), 3000);
  if (m.t === 'end') process.exit(0);
});
ws.on('close', () => { console.log('closed'); process.exit(0); });
