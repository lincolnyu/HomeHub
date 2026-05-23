"use strict";

const TAU = Math.PI * 2;
const EPS = 1e-9;
const MAX_COLLISION_LOOKAHEAD = 30;
const DEFAULT_BOX = {
  space: "x < 5 && x > -5 && y < 5 && y > -5 && z < 5 && z > -5",
  subspaces: "x < 0\nx >= 0 && y < 0",
  bounds: { minX: -5, maxX: 5, minY: -5, maxY: 5, minZ: -5, maxZ: 5 },
};
const DEFAULT_SPHERE = {
  space: "x^2 + y^2 + z^2 <= 25",
  subspaces: "x < 0\ny < 0",
  bounds: { minX: -5, maxX: 5, minY: -5, maxY: 5, minZ: -5, maxZ: 5 },
};
const SUBSPACE_PALETTE = [
  "#00d1ff",
  "#ff4f8b",
  "#ffe45e",
  "#3df277",
  "#b56cff",
  "#ff8a2a",
  "#00e0c6",
  "#f04444",
  "#8ad8ff",
  "#d6ff4f",
  "#ff73e1",
  "#7f9cff",
];

const els = {
  canvas: document.querySelector("#projection"),
  timeReadout: document.querySelector("#timeReadout"),
  collisionReadout: document.querySelector("#collisionReadout"),
  particleReadout: document.querySelector("#particleReadout"),
  spaceInput: document.querySelector("#spaceInput"),
  subspaceInput: document.querySelector("#subspaceInput"),
  particleRadius: document.querySelector("#particleRadius"),
  collisionThreshold: document.querySelector("#collisionThreshold"),
  histogramBins: document.querySelector("#histogramBins"),
  playbackSpeed: document.querySelector("#playbackSpeed"),
  subspaceSettings: document.querySelector("#subspaceSettings"),
  loadSphere: document.querySelector("#loadSphere"),
  loadBox: document.querySelector("#loadBox"),
  randomize: document.querySelector("#randomize"),
  playPause: document.querySelector("#playPause"),
  stepOne: document.querySelector("#stepOne"),
  stepMany: document.querySelector("#stepMany"),
  reset: document.querySelector("#reset"),
  message: document.querySelector("#message"),
  histograms: document.querySelector("#histograms"),
};

const ctx = els.canvas.getContext("2d");
let state;
let initialParticles = [];
let config;
let compiled = {};
let playing = false;
let lastFrame = performance.now();
let subspaceSettingsState = [];

function expressionToFunction(source) {
  const normalized = source
    .replace(/\bAND\b/gi, "&&")
    .replace(/\bOR\b/gi, "||")
    .replace(/\bNOT\b/gi, "!")
    .replace(/\^/g, "**")
    .replace(/\b(sin|cos|tan|sqrt|abs|min|max|pow|exp|log)\s*\(/g, "Math.$1(");
  return new Function("x", "y", "z", `"use strict"; return Boolean(${normalized});`);
}

function numericExpression(source) {
  const normalized = source
    .replace(/\^/g, "**")
    .replace(/\b(sin|cos|tan|sqrt|abs|min|max|pow|exp|log)\s*\(/g, "Math.$1(");
  return new Function("x", "y", "z", `"use strict"; return Number(${normalized});`);
}

function splitInequalities(source) {
  return source
    .split(/&&|\n/)
    .map((part) => part.trim())
    .filter(Boolean);
}

function compileConstraintSides(source) {
  return splitInequalities(source).map((part) => {
    const match = part.match(/^(.*?)(<=|>=|<|>)(.*)$/);
    if (!match) return null;
    const left = numericExpression(match[1]);
    const right = numericExpression(match[3]);
    const sign = match[2].includes("<") ? 1 : -1;
    return (p) => sign * (left(p.x, p.y, p.z) - right(p.x, p.y, p.z));
  }).filter(Boolean);
}

function readConfig() {
  const subspaceRules = els.subspaceInput.value
    .split("\n")
    .map((line) => line.trim())
    .filter(Boolean);
  const radius = Number(els.particleRadius.value);
  ensureSubspaceSettings(subspaceRules.length + 1);
  return {
    spaceSource: els.spaceInput.value.trim(),
    subspaceSources: subspaceRules,
    particleRadius: radius,
    threshold: Number(els.collisionThreshold.value),
    histogramBins: Number(els.histogramBins.value),
    playbackSpeed: Number(els.playbackSpeed.value),
    subspaceSettings: readSubspaceSettings(subspaceRules.length + 1),
    bounds: inferBounds(els.spaceInput.value.trim()),
  };
}

function ensureSubspaceSettings(count) {
  while (subspaceSettingsState.length < count) {
    subspaceSettingsState.push({ particles: 12, speedCap: 4 });
  }
  if (subspaceSettingsState.length > count) {
    subspaceSettingsState = subspaceSettingsState.slice(0, count);
  }
}

function readSubspaceSettings(count) {
  const rows = [...els.subspaceSettings.querySelectorAll(".subspace-row")];
  if (rows.length === count) {
    subspaceSettingsState = rows.map((row, index) => ({
      particles: Math.max(0, Number(row.querySelector("[data-field='particles']").value) || 0),
      speedCap: Math.max(0.1, Number(row.querySelector("[data-field='speedCap']").value) || subspaceSettingsState[index]?.speedCap || 4),
    }));
  } else {
    ensureSubspaceSettings(count);
  }
  return subspaceSettingsState.map((setting) => ({ ...setting }));
}

function inferBounds(source) {
  if (/x\s*\^\s*2\s*\+\s*y\s*\^\s*2\s*\+\s*z\s*\^\s*2\s*<=\s*([0-9.]+)/.test(source)) {
    const r = Math.sqrt(Number(RegExp.$1));
    return { minX: -r, maxX: r, minY: -r, maxY: r, minZ: -r, maxZ: r };
  }
  const bounds = { minX: -5, maxX: 5, minY: -5, maxY: 5, minZ: -5, maxZ: 5 };
  for (const axis of ["x", "y", "z"]) {
    const upper = [...source.matchAll(new RegExp(`${axis}\\s*<\\s*=?\\s*(-?[0-9.]+)`, "g"))].map((m) => Number(m[1]));
    const lower = [...source.matchAll(new RegExp(`${axis}\\s*>\\s*=?\\s*(-?[0-9.]+)`, "g"))].map((m) => Number(m[1]));
    if (upper.length) bounds[`max${axis.toUpperCase()}`] = Math.min(...upper);
    if (lower.length) bounds[`min${axis.toUpperCase()}`] = Math.max(...lower);
  }
  return bounds;
}

function compileAll() {
  config = readConfig();
  compiled.space = expressionToFunction(config.spaceSource);
  compiled.subspaces = config.subspaceSources.map(expressionToFunction);
  compiled.constraints = compileConstraintSides(config.spaceSource);
  compiled.isInside = (p) => compiled.space(p.x, p.y, p.z);
}

function randomInSpace() {
  const b = config.bounds;
  for (let attempts = 0; attempts < 10000; attempts += 1) {
    const p = {
      x: randomBetween(b.minX + config.particleRadius, b.maxX - config.particleRadius),
      y: randomBetween(b.minY + config.particleRadius, b.maxY - config.particleRadius),
      z: randomBetween(b.minZ + config.particleRadius, b.maxZ - config.particleRadius),
    };
    if (compiled.isInside(p)) return p;
  }
  throw new Error("Could not place particles inside this space.");
}

function randomBetween(min, max) {
  return min + Math.random() * (max - min);
}

function randomVelocity(speedCap) {
  const speed = randomBetween(speedCap * 0.25, speedCap);
  const theta = Math.random() * TAU;
  const u = randomBetween(-1, 1);
  const planar = Math.sqrt(1 - u * u);
  return {
    x: speed * planar * Math.cos(theta),
    y: speed * planar * Math.sin(theta),
    z: speed * u,
  };
}

function generateParticles() {
  compileAll();
  const particles = [];
  const targets = config.subspaceSettings.map((setting) => setting.particles);
  const accepted = Array(targets.length).fill(0);
  const totalTarget = targets.reduce((sum, value) => sum + value, 0);
  let attempts = 0;
  const maxAttempts = Math.max(20000, totalTarget * 5000);
  while (particles.length < totalTarget && attempts < maxAttempts) {
    attempts += 1;
    const position = randomInSpace();
    const subspace = classifyPoint(position);
    if (accepted[subspace] >= targets[subspace]) continue;
    const overlaps = particles.some((p) => distance(p.position, position) < config.particleRadius * 2.15);
    if (!overlaps) {
      const id = particles.length;
      const speedCap = config.subspaceSettings[subspace].speedCap;
      particles.push({ id, subspace, position, velocity: randomVelocity(speedCap), color: colorForSubspace(subspace) });
      accepted[subspace] += 1;
    }
  }
  if (particles.length < totalTarget) {
    const missing = targets
      .map((target, index) => target - accepted[index])
      .map((count, index) => count > 0 ? `Subspace ${index + 1}: ${count}` : "")
      .filter(Boolean)
      .join(", ");
    throw new Error(`Could not place all requested particles. Remaining: ${missing}.`);
  }
  initialParticles = cloneParticles(particles);
  state = { particles, time: 0, collisions: 0 };
}

function cloneParticles(particles) {
  return particles.map((p) => ({
    id: p.id,
    subspace: p.subspace,
    color: p.color,
    position: { ...p.position },
    velocity: { ...p.velocity },
  }));
}

function colorForSubspace(index) {
  if (index < SUBSPACE_PALETTE.length) return SUBSPACE_PALETTE[index];
  const hue = (index * 137.508) % 360;
  return `hsl(${hue} 92% 62%)`;
}

function distance(a, b) {
  return Math.hypot(a.x - b.x, a.y - b.y, a.z - b.z);
}

function dot(a, b) {
  return a.x * b.x + a.y * b.y + a.z * b.z;
}

function add(a, b, scale = 1) {
  return { x: a.x + b.x * scale, y: a.y + b.y * scale, z: a.z + b.z * scale };
}

function sub(a, b) {
  return { x: a.x - b.x, y: a.y - b.y, z: a.z - b.z };
}

function scale(v, s) {
  return { x: v.x * s, y: v.y * s, z: v.z * s };
}

function norm(v) {
  const length = Math.hypot(v.x, v.y, v.z) || 1;
  return scale(v, 1 / length);
}

function advanceParticles(particles, dt) {
  for (const p of particles) {
    p.position = add(p.position, p.velocity, dt);
  }
}

function getNextCollisionEvent() {
  const pairCandidates = [];
  let earliest = Infinity;
  const targetDistance = config.particleRadius * 2 + config.threshold;
  for (let i = 0; i < state.particles.length; i += 1) {
    for (let j = i + 1; j < state.particles.length; j += 1) {
      const a = state.particles[i];
      const b = state.particles[j];
      const dp = sub(a.position, b.position);
      const dv = sub(a.velocity, b.velocity);
      const c = dot(dp, dp) - targetDistance * targetDistance;
      const bTerm = 2 * dot(dp, dv);
      const aTerm = dot(dv, dv);
      if (aTerm < EPS || bTerm >= 0) continue;
      const disc = bTerm * bTerm - 4 * aTerm * c;
      if (disc < 0) continue;
      const t = (-bTerm - Math.sqrt(disc)) / (2 * aTerm);
      if (t >= -EPS && t < earliest - EPS) {
        earliest = Math.max(0, t);
        pairCandidates.length = 0;
        pairCandidates.push([i, j, Math.sqrt(Math.max(0, dot(dp, dp)))]);
      } else if (Number.isFinite(earliest) && Math.abs(t - earliest) < 0.002) {
        pairCandidates.push([i, j, Math.sqrt(Math.max(0, dot(dp, dp)))]);
      }
    }
  }

  const wallEvent = getNextBoundaryEvent();
  if (wallEvent && wallEvent.t < earliest - EPS) {
    return { t: wallEvent.t, pairs: [], walls: [wallEvent] };
  }
  if (wallEvent && Number.isFinite(earliest) && Math.abs(wallEvent.t - earliest) < 0.002) {
    return { t: earliest, pairs: pruneCollisionPairs(pairCandidates), walls: [wallEvent] };
  }
  if (!Number.isFinite(earliest)) {
    return wallEvent ? { t: wallEvent.t, pairs: [], walls: [wallEvent] } : null;
  }
  return { t: earliest, pairs: pruneCollisionPairs(pairCandidates), walls: [] };
}

function getNextBoundaryEvent() {
  let best = null;
  for (let i = 0; i < state.particles.length; i += 1) {
    const p = state.particles[i];
    const t = findBoundaryTime(p);
    if (t !== null && (!best || t < best.t)) best = { t, index: i };
  }
  return best;
}

function findBoundaryTime(particle) {
  const probe = (t) => compiled.isInside(add(particle.position, particle.velocity, t));
  if (!probe(0)) return 0;
  let lo = 0;
  let hi = 1 / 60;
  while (hi < MAX_COLLISION_LOOKAHEAD && probe(hi)) {
    lo = hi;
    hi *= 1.7;
  }
  if (hi >= MAX_COLLISION_LOOKAHEAD && probe(hi)) return null;
  for (let i = 0; i < 36; i += 1) {
    const mid = (lo + hi) / 2;
    if (probe(mid)) lo = mid;
    else hi = mid;
  }
  return Math.max(0, lo);
}

function pruneCollisionPairs(candidates) {
  const sorted = [...candidates].sort((a, b) => a[2] - b[2]);
  const used = new Set();
  const pairs = [];
  for (const [i, j] of sorted) {
    if (!used.has(i) && !used.has(j)) {
      used.add(i);
      used.add(j);
      pairs.push([i, j]);
    }
  }
  return pairs;
}

function executeCollisionEvent(event) {
  for (const [i, j] of event.pairs) {
    collideParticles(state.particles[i], state.particles[j]);
  }
  for (const wall of event.walls) {
    reflectFromBoundary(state.particles[wall.index]);
  }
  if (event.pairs.length || event.walls.length) state.collisions += event.pairs.length + event.walls.length;
}

function collideParticles(a, b) {
  const n = norm(sub(a.position, b.position));
  const vaN = dot(a.velocity, n);
  const vbN = dot(b.velocity, n);
  a.velocity = add(a.velocity, n, vbN - vaN);
  b.velocity = add(b.velocity, n, vaN - vbN);
  separatePair(a, b);
}

function separatePair(a, b) {
  const delta = sub(a.position, b.position);
  const d = Math.hypot(delta.x, delta.y, delta.z) || 1;
  const overlap = config.particleRadius * 2 - d + EPS;
  if (overlap <= 0) return;
  const n = scale(delta, 1 / d);
  a.position = add(a.position, n, overlap / 2);
  b.position = add(b.position, n, -overlap / 2);
}

function reflectFromBoundary(particle) {
  const normal = boundaryNormal(particle.position);
  const vn = dot(particle.velocity, normal);
  if (vn < 0) particle.velocity = add(particle.velocity, normal, -2 * vn);
  particle.position = add(particle.position, normal, config.threshold + EPS);
}

function boundaryNormal(point) {
  if (!compiled.constraints.length) return norm(scale(point, -1));
  let active = compiled.constraints[0];
  let activeValue = -Infinity;
  for (const fn of compiled.constraints) {
    const value = fn(point);
    if (value > activeValue) {
      activeValue = value;
      active = fn;
    }
  }
  const h = 1e-4;
  const gx = active({ x: point.x + h, y: point.y, z: point.z }) - active({ x: point.x - h, y: point.y, z: point.z });
  const gy = active({ x: point.x, y: point.y + h, z: point.z }) - active({ x: point.x, y: point.y - h, z: point.z });
  const gz = active({ x: point.x, y: point.y, z: point.z + h }) - active({ x: point.x, y: point.y, z: point.z - h });
  return norm({ x: -gx, y: -gy, z: -gz });
}

function stepCollision(count = 1) {
  try {
    compileAll();
    for (let i = 0; i < count; i += 1) {
      const event = getNextCollisionEvent();
      if (!event) {
        setMessage("No impending collisions found inside the lookahead window.");
        break;
      }
      advanceParticles(state.particles, event.t);
      state.time += event.t;
      executeCollisionEvent(event);
    }
    render();
  } catch (error) {
    setMessage(error.message, true);
  }
}

function simulateContinuous(dt) {
  let remaining = dt * config.playbackSpeed;
  let guard = 0;
  while (remaining > EPS && guard < 100) {
    const event = getNextCollisionEvent();
    if (!event || event.t > remaining) {
      advanceParticles(state.particles, remaining);
      state.time += remaining;
      remaining = 0;
    } else {
      advanceParticles(state.particles, event.t);
      state.time += event.t;
      remaining -= event.t;
      executeCollisionEvent(event);
    }
    guard += 1;
  }
}

function classifyPoint(point) {
  const { x, y, z } = point;
  for (let i = 0; i < compiled.subspaces.length; i += 1) {
    if (compiled.subspaces[i](x, y, z)) return i;
  }
  return compiled.subspaces.length;
}

function classifySubspace(particle) {
  return classifyPoint(particle.position);
}

function speed(particle) {
  return Math.hypot(particle.velocity.x, particle.velocity.y, particle.velocity.z);
}

function render() {
  drawProjection();
  drawHistograms();
  els.timeReadout.textContent = `t = ${state.time.toFixed(3)} s`;
  els.collisionReadout.textContent = `collisions = ${state.collisions}`;
  els.particleReadout.textContent = `particles = ${state.particles.length}`;
}

function drawProjection() {
  const width = els.canvas.width;
  const height = els.canvas.height;
  ctx.clearRect(0, 0, width, height);
  ctx.fillStyle = "#0b0d0e";
  ctx.fillRect(0, 0, width, height);
  drawGrid();

  const b = config.bounds;
  const span = Math.max(b.maxX - b.minX, b.maxY - b.minY);
  const pad = 44;
  const scaleFactor = (width - pad * 2) / span;
  const toCanvas = (p) => ({
    x: pad + (p.x - b.minX) * scaleFactor,
    y: height - pad - (p.y - b.minY) * scaleFactor,
  });
  for (const p of state.particles) {
    const c = toCanvas(p.position);
    const r = Math.max(3, config.particleRadius * scaleFactor);
    ctx.beginPath();
    ctx.arc(c.x, c.y, r, 0, TAU);
    ctx.fillStyle = p.color;
    ctx.globalAlpha = 0.88;
    ctx.fill();
    ctx.globalAlpha = 1;
    ctx.strokeStyle = "#ffffff99";
    ctx.lineWidth = 1;
    ctx.stroke();
  }
}

function drawGrid() {
  const width = els.canvas.width;
  const height = els.canvas.height;
  ctx.strokeStyle = "#202628";
  ctx.lineWidth = 1;
  for (let i = 0; i <= 10; i += 1) {
    const x = (i / 10) * width;
    const y = (i / 10) * height;
    ctx.beginPath();
    ctx.moveTo(x, 0);
    ctx.lineTo(x, height);
    ctx.moveTo(0, y);
    ctx.lineTo(width, y);
    ctx.stroke();
  }
  ctx.strokeStyle = "#536064";
  ctx.beginPath();
  ctx.moveTo(width / 2, 0);
  ctx.lineTo(width / 2, height);
  ctx.moveTo(0, height / 2);
  ctx.lineTo(width, height / 2);
  ctx.stroke();
}

function drawHistograms() {
  const subspaceCount = compiled.subspaces.length + 1;
  const bins = Array.from({ length: subspaceCount }, () => Array(config.histogramBins).fill(0));
  const totals = Array(subspaceCount).fill(0);
  const histogramMax = getGlobalHistogramMax();
  for (const p of state.particles) {
    const group = classifySubspace(p);
    const index = Math.min(config.histogramBins - 1, Math.floor(speed(p) / histogramMax * config.histogramBins));
    bins[group][Math.max(0, index)] += 1;
    totals[group] += 1;
  }
  els.histograms.innerHTML = "";
  bins.forEach((counts, group) => {
    const maxCount = Math.max(1, ...counts);
    const wrapper = document.createElement("div");
    wrapper.className = "histogram";
    const title = document.createElement("div");
    title.className = "histogram-title";
    title.innerHTML = `<span>Subspace ${group + 1}</span><span>${totals[group]} particles</span>`;
    const bars = document.createElement("div");
    bars.className = "bars";
    counts.forEach((count, i) => {
      const lo = (i * histogramMax / config.histogramBins).toFixed(2);
      const hi = ((i + 1) * histogramMax / config.histogramBins).toFixed(2);
      const row = document.createElement("div");
      row.className = "bar-row";
      row.innerHTML = `<span>${lo}-${hi}</span><span class="bar-track"><span class="bar-fill" style="width:${count / maxCount * 100}%"></span></span><span>${count}</span>`;
      bars.appendChild(row);
    });
    wrapper.append(title, bars);
    els.histograms.appendChild(wrapper);
  });
}

function getGlobalHistogramMax() {
  const currentMax = state.particles.reduce((max, particle) => Math.max(max, speed(particle)), 0);
  const configuredMax = config.subspaceSettings.reduce((max, setting) => Math.max(max, setting.speedCap), 0);
  return Math.max(0.1, currentMax, configuredMax);
}

function resetToInitial() {
  compileAll();
  state = { particles: cloneParticles(initialParticles), time: 0, collisions: 0 };
  render();
}

function loadPreset(preset) {
  els.spaceInput.value = preset.space;
  els.subspaceInput.value = preset.subspaces;
  Object.assign(config || {}, { bounds: preset.bounds });
  syncSubspaceSettingsPanel();
  randomize();
}

function randomize() {
  try {
    setMessage("");
    generateParticles();
    render();
  } catch (error) {
    setMessage(error.message, true);
  }
}

function setMessage(text, error = false) {
  els.message.textContent = text;
  els.message.classList.toggle("error", error);
}

function syncSubspaceSettingsPanel() {
  const subspaceRules = els.subspaceInput.value
    .split("\n")
    .map((line) => line.trim())
    .filter(Boolean);
  readSubspaceSettings(subspaceRules.length + 1);
  ensureSubspaceSettings(subspaceRules.length + 1);
  els.subspaceSettings.innerHTML = "";
  subspaceSettingsState.forEach((setting, index) => {
    const row = document.createElement("div");
    row.className = "subspace-row";
    const rule = subspaceRules[index] || "remaining space";
    row.innerHTML = `
      <div class="subspace-name">
        <span class="subspace-swatch" style="background:${colorForSubspace(index)}"></span>
        Subspace ${index + 1}
        <span class="subspace-rule">${rule}</span>
      </div>
      <label>
        Particles
        <input data-field="particles" data-index="${index}" type="number" min="0" max="200" step="1" value="${setting.particles}">
      </label>
      <label>
        Initial speed cap
        <input data-field="speedCap" data-index="${index}" type="number" min="0.1" max="20" step="0.1" value="${setting.speedCap}">
      </label>
    `;
    els.subspaceSettings.appendChild(row);
  });
}

function tick(now) {
  const dt = Math.min(0.05, (now - lastFrame) / 1000);
  lastFrame = now;
  if (playing) {
    try {
      compileAll();
      simulateContinuous(dt);
      render();
    } catch (error) {
      playing = false;
      updatePlayButton();
      setMessage(error.message, true);
    }
  }
  requestAnimationFrame(tick);
}

function updatePlayButton() {
  els.playPause.textContent = playing ? "Pause" : "Play";
  els.playPause.classList.toggle("playing", playing);
}

function initInputs() {
  els.spaceInput.value = DEFAULT_SPHERE.space;
  els.subspaceInput.value = DEFAULT_SPHERE.subspaces;
  els.particleRadius.value = 0.18;
  els.collisionThreshold.value = 0.01;
  els.histogramBins.value = 10;
  els.playbackSpeed.value = 1;
  subspaceSettingsState = [
    { particles: 12, speedCap: 4 },
    { particles: 12, speedCap: 4 },
    { particles: 12, speedCap: 4 },
  ];
  syncSubspaceSettingsPanel();
}

els.loadSphere.addEventListener("click", () => loadPreset(DEFAULT_SPHERE));
els.loadBox.addEventListener("click", () => loadPreset(DEFAULT_BOX));
els.randomize.addEventListener("click", randomize);
els.reset.addEventListener("click", resetToInitial);
els.stepOne.addEventListener("click", () => stepCollision(1));
els.stepMany.addEventListener("click", () => stepCollision(10));
els.playPause.addEventListener("click", () => {
  playing = !playing;
  lastFrame = performance.now();
  updatePlayButton();
});

els.subspaceInput.addEventListener("change", () => {
  try {
    syncSubspaceSettingsPanel();
    compileAll();
    render();
    setMessage("");
  } catch (error) {
    setMessage(error.message, true);
  }
});

els.subspaceSettings.addEventListener("change", () => {
  try {
    compileAll();
    render();
    setMessage("");
  } catch (error) {
    setMessage(error.message, true);
  }
});

for (const input of [els.spaceInput, els.histogramBins, els.particleRadius, els.collisionThreshold, els.playbackSpeed]) {
  input.addEventListener("change", () => {
    try {
      compileAll();
      render();
      setMessage("");
    } catch (error) {
      setMessage(error.message, true);
    }
  });
}

initInputs();
randomize();
requestAnimationFrame(tick);
