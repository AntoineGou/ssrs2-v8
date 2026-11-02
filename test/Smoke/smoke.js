(function () {
  const out = [];
  // toFixed, plain and as a number inside a hot loop (TurboFan's fold)
  out.push((1.005).toFixed(2), (1.45).toFixed(1), (-0.5).toFixed(0), (123.456).toFixed(10), (1e21).toFixed(2));
  let s = 0;
  for (let i = 0; i < 300000; i++) s += +((i * 0.37).toFixed(2)) - +((i / 7).toFixed(i % 21));
  out.push(s.toFixed(4));
  // dates: Date.UTC folded, new Date(number), setUTCDate, comparisons of dates
  let n = 0;
  for (let k = 0; k < 2000; k++) {
    const d = new Date(Date.UTC(2026, 0, 1));
    while (d <= new Date(Date.UTC(2026, 9, 15))) { d.setUTCDate(d.getUTCDate() + 1); n++; }
  }
  out.push(String(n), new Date(Date.UTC(2026, 1, 29)).toISOString(), String(new Date(Date.UTC(2026, 9, 15)) - new Date(Date.UTC(2026, 0, 1))));
  // decimals made numbers
  out.push(String(Number("-123.45") * 2), String(+"0.1" + +"0.2"), String(parseFloat("3.14159265358979")));
  // decimals read by JSON.parse, as Number reads them (random digits, signs, leading zeros of fractions, past 15 digits)
  let seed = 7, unlike = 0;
  const rnd = () => (seed = (Math.imul(seed, 1103515245) + 12345) >>> 0) >>> 8;
  const texts = [];
  for (let i = 0; i < 100000; i++) {
    let ip = ''; for (let k = rnd() % 12; k > 0; k--) ip += rnd() % 10;
    ip = ip.replace(/^0+/, '') || '0';
    let text = (rnd() % 3 === 0 ? '-' : '') + ip;
    const f = rnd() % 24; if (f > 0) { text += '.'; for (let k = 0; k < f; k++) text += rnd() % 10; }
    texts.push(text);
  }
  const values = JSON.parse('[' + texts.join(',') + ']');
  for (let i = 0; i < texts.length; i++) if (!Object.is(values[i], Number(texts[i]))) unlike++;
  const edge = JSON.parse('[0.1, -0, -0.0, 5.0, 1e5, 123456789012345.6, 0.0000000000000000000001, 2147483648.5]');
  out.push(String(unlike), edge.map(v => Object.is(v, -0) ? '-0' : String(v)).join(','));
  // polymorphic objects (inline caches past four maps)
  class E { constructor() { this.x = 1; this.y = 2; } }
  const kinds = [];
  for (let c = 0; c < 12; c++) kinds.push(class extends E { constructor() { super(); this["k" + c] = c; } });
  let t = 0;
  for (let i = 0; i < 120000; i++) { const o = new kinds[i % 12](); o.x = i; t += o.x + o.y; }
  out.push(String(t));
  // Intl with the library's own ICU data
  out.push(new Intl.NumberFormat("de-CH").format(1234567.891), new Intl.DateTimeFormat("fr-FR", { timeZone: "UTC", dateStyle: "long" }).format(new Date(Date.UTC(2026, 9, 15))));
  return out.join("|");
})()
