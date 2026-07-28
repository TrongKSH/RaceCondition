const pptxgen = require('pptxgenjs');
const pres = new pptxgen();
pres.layout = 'LAYOUT_WIDE';               // 13.3 x 7.5
pres.author = 'Rohan';
pres.title = 'Race Conditions in EF Core + SQL Server';

// ---- palette -------------------------------------------------------------
const BG   = '0B0F17';
const CARD = '18212F';
const CARD2= '111926';
const CODE = '070B12';
const TXT  = 'E9F0FA';
const DIM  = '93A6C2';
const CYAN = '38BDF8';
const RED  = 'F87171';
const GRN  = '4ADE80';
const AMB  = 'FBBF24';
const VIO  = 'C084FC';
const B    = '283549';    // border / hairline colour
const SANS = 'Calibri';
const MONO = 'Courier New';
const SERIF= 'Cambria';

function slide(){ const s = pres.addSlide(); s.background = { color: BG }; return s; }

function title(s, t, sub){
  s.addText(t, { x:0.6, y:0.42, w:12.1, h:0.62, fontSize:34, bold:true, color:TXT,
                 fontFace:SERIF, margin:0 });
  if (sub) s.addText(sub, { x:0.62, y:1.06, w:12.1, h:0.36, fontSize:14.5, color:DIM,
                            fontFace:SANS, margin:0 });
}

function card(s, x, y, w, h, fill, border){
  s.addShape(pres.ShapeType.roundRect, {
    x, y, w, h, rectRadius:0.09, fill:{ color: fill || CARD },
    line:{ color: border || B, width:1 }
  });
}

function code(s, x, y, w, h, lines, size){
  s.addShape(pres.ShapeType.roundRect, {
    x, y, w, h, rectRadius:0.07, fill:{ color:CODE }, line:{ color:B, width:1 }
  });
  s.addText(lines, { x:x+0.18, y:y+0.14, w:w-0.36, h:h-0.28, fontSize:size||11.5,
                     fontFace:MONO, color:TXT, margin:0, valign:'top', lineSpacingMultiple:1.12 });
}

function chip(s, x, y, w, label, val, col){
  card(s, x, y, w, 1.12, CARD2);
  s.addText(val, { x:x+0.16, y:y+0.1, w:w-0.32, h:0.6, fontSize:30, bold:true,
                   color:col, fontFace:MONO, margin:0, valign:'middle' });
  s.addText(label,{ x:x+0.18, y:y+0.7, w:w-0.32, h:0.32, fontSize:10.5, color:DIM,
                    fontFace:SANS, margin:0, charSpacing:1 });
}

function dot(s, x, y, n, col){
  s.addShape(pres.ShapeType.ellipse, { x, y, w:0.34, h:0.34, fill:{color:col+''},
    line:{color:col, width:0} });
  s.addText(String(n), { x, y, w:0.34, h:0.34, fontSize:12, bold:true, color:BG,
    align:'center', valign:'middle', fontFace:MONO, margin:0 });
}

function foot(s, t){
  s.addText(t, { x:0.62, y:6.92, w:12.1, h:0.3, fontSize:10.5, color:DIM,
                 fontFace:SANS, italic:true, margin:0 });
}

/* ========================================================================
   1 · TITLE
   ======================================================================== */
{
  const s = slide();
  s.addText('Race Conditions in', { x:0.9, y:1.75, w:11.5, h:0.8, fontSize:40,
    color:DIM, fontFace:SERIF, margin:0 });
  s.addText('EF Core + SQL Server', { x:0.9, y:2.45, w:11.5, h:1.0, fontSize:56,
    bold:true, color:TXT, fontFace:SERIF, margin:0 });
  s.addText('The bug that returns 200 OK, writes no log, and takes your money anyway.',
    { x:0.92, y:3.55, w:11.3, h:0.5, fontSize:17, color:CYAN, fontFace:SANS, margin:0 });

  code(s, 0.9, 4.35, 7.2, 1.05, [
    { text:'UPDATE Wallets SET Balance = 0\n', options:{ color:RED } },
    { text:'WHERE Id = 1;', options:{ color:RED } },
    { text:'                      -- twice. both succeed.', options:{ color:DIM } }
  ], 13);

  s.addText('Sharing session  ·  30 minutes  ·  live demo', { x:8.5, y:5.3, w:4.0,
    h:0.4, fontSize:12.5, color:DIM, fontFace:SANS, align:'right', margin:0 });
  s.addNotes('Do not introduce yourself yet. Open straight on the simulator and ask the room the $100 question.');
}

/* ========================================================================
   2 · THE QUESTION
   ======================================================================== */
{
  const s = slide();
  title(s, 'Alice has exactly $100.', 'She taps "Withdraw $100" on her phone. Her laptop, still logged in, sends the same request 8 ms later.');

  card(s, 0.6, 1.8, 3.9, 1.9);
  s.addText('Request A', { x:0.85, y:1.98, w:3.4, h:0.35, fontSize:16, bold:true, color:CYAN, fontFace:SANS, margin:0 });
  s.addText('POST /wallets/1/withdraw\n{ "amount": 100 }', { x:0.85, y:2.42, w:3.4, h:0.9,
    fontSize:12, color:TXT, fontFace:MONO, margin:0 });

  card(s, 0.6, 3.95, 3.9, 1.9);
  s.addText('Request B', { x:0.85, y:4.13, w:3.4, h:0.35, fontSize:16, bold:true, color:VIO, fontFace:SANS, margin:0 });
  s.addText('POST /wallets/1/withdraw\n{ "amount": 100 }', { x:0.85, y:4.57, w:3.4, h:0.9,
    fontSize:12, color:TXT, fontFace:MONO, margin:0 });

  s.addShape(pres.ShapeType.line, { x:4.6, y:2.75, w:1.5, h:1.15,
    line:{ color:CYAN, width:2, endArrowType:'triangle' } });
  s.addShape(pres.ShapeType.line, { x:4.6, y:4.9, w:1.5, h:-1.15,
    line:{ color:VIO, width:2, endArrowType:'triangle' } });

  card(s, 6.3, 2.55, 6.4, 2.6, CARD2, CYAN);
  s.addText('dbo.Wallets  ·  Id = 1', { x:6.6, y:2.75, w:5.8, h:0.3, fontSize:11.5,
    color:DIM, fontFace:MONO, margin:0, charSpacing:1 });
  s.addText('$100.00', { x:6.6, y:3.1, w:5.8, h:1.0, fontSize:54, bold:true, color:TXT,
    fontFace:MONO, margin:0 });
  s.addText('One row. Two threads. No coordination.', { x:6.6, y:4.35, w:5.8, h:0.4,
    fontSize:14, color:DIM, fontFace:SANS, margin:0 });

  s.addText('How much money does she get?', { x:0.6, y:6.15, w:12.1, h:0.55, fontSize:26,
    bold:true, color:AMB, fontFace:SERIF, margin:0 });
  s.addNotes('Show of hands: $100 or $200. Do not say the words "race condition" yet.');
}

/* ========================================================================
   3 · THE ANSWER
   ======================================================================== */
{
  const s = slide();
  title(s, 'She gets $200.', 'And nothing anywhere tells you it happened.');

  chip(s, 0.6, 1.9, 2.9, 'HTTP responses', '200 · 200', GRN);
  chip(s, 3.7, 1.9, 2.9, 'Exceptions thrown', '0', RED);
  chip(s, 6.8, 1.9, 2.9, 'Log lines written', '0', RED);
  chip(s, 9.9, 1.9, 2.8, 'Error rate', '0.0%', RED);

  card(s, 0.6, 3.35, 12.1, 1.75, CARD2);
  s.addText([
    { text:'Every other bug you ship pages you.  ', options:{ color:TXT, fontSize:19, bold:true } },
    { text:'This one does not.\n\n', options:{ color:RED, fontSize:19, bold:true } },
    { text:'A lost update raises no exception and writes no log. Your APM dashboard stays green. '
         + 'You find it at month-end close, in a ledger that no longer reconciles — weeks after the money left.',
      options:{ color:DIM, fontSize:15 } }
  ], { x:0.9, y:3.55, w:11.5, h:1.35, fontFace:SANS, margin:0, valign:'top' });

  s.addText('"It has never happened in production" is not evidence. It is the absence of an alert that was never going to fire.',
    { x:0.6, y:5.45, w:12.1, h:0.6, fontSize:16, italic:true, color:AMB, fontFace:SERIF, margin:0 });
  s.addNotes('Land this hard. The silence is what makes this bug class different.');
}

/* ========================================================================
   4 · STARBUCKS
   ======================================================================== */
{
  const s = slide();
  title(s, 'Starbucks gift cards — May 2015',
    'Egor Homakov, Sakurity. Total cost of the exploit: three $5 cards and two browser windows.');

  chip(s, 0.6, 1.75, 2.9, 'ACTUALLY PAID', '$15', CYAN);
  chip(s, 3.7, 1.75, 2.9, 'BALANCE AFTER', '$20', RED);
  chip(s, 6.8, 1.75, 2.9, 'BROWSERS NEEDED', '2', AMB);
  chip(s, 9.9, 1.75, 2.8, 'DAYS TO GET A REPLY', '37', VIO);

  const steps = [
    ['Bought three gift cards, $5 each. $15 in the system.', TXT],
    ['Opened the transfer page in two browsers, separate session cookies.', TXT],
    ['Fired "transfer $5 from card 1 to card 2" from both, simultaneously.', TXT],
    ['The balance check ran twice before either write landed. Both passed.', RED],
    ['Both transfers credited card 2. $10 from a single $5 balance.', RED],
    ['Spent $16.70 in-store to prove it was real, then repaid it.', TXT]
  ];
  let y = 3.15;
  steps.forEach((st, i) => {
    dot(s, 0.65, y, i+1, st[1] === RED ? RED : CYAN);
    s.addText(st[0], { x:1.15, y:y-0.03, w:11.4, h:0.4, fontSize:14.5,
      color:st[1], fontFace:SANS, margin:0, valign:'middle' });
    y += 0.52;
  });

  foot(s, 'Reported 23 March. First reply 29 April. Starbucks\' public statement called it "fraudulent activity".');
  s.addNotes('This is the funny one. Emphasise how small the attack kit was.');
}

/* ========================================================================
   5 · FLEXCOIN
   ======================================================================== */
{
  const s = slide();
  title(s, 'Flexcoin — 2 March 2014',
    '"The Bitcoin Bank." Same bug class. The company did not survive the week.');

  chip(s, 0.6, 1.75, 2.9, 'BITCOIN DRAINED', '896', RED);
  chip(s, 3.7, 1.75, 2.9, 'VALUE AT THE TIME', '~$600k', RED);
  chip(s, 6.8, 1.75, 2.9, 'CONCURRENT REQUESTS', '1000s', AMB);
  chip(s, 9.9, 1.75, 2.8, 'EXPLOIT TO SHUTDOWN', '2 days', VIO);

  card(s, 0.6, 3.2, 6.0, 3.05);
  s.addText('What the attacker did', { x:0.85, y:3.38, w:5.5, h:0.35, fontSize:16,
    bold:true, color:CYAN, fontFace:SANS, margin:0 });
  s.addText([
    { text:'Funded one account normally.', options:{ bullet:true, breakLine:true } },
    { text:'Fired thousands of concurrent internal transfers moving the same balance out.', options:{ bullet:true, breakLine:true } },
    { text:'Every request read the balance before any other had written. Every check passed.', options:{ bullet:true, breakLine:true, color:RED } },
    { text:'Each transfer credited the destination; the source decremented once.', options:{ bullet:true, breakLine:true, color:RED } },
    { text:'Repeated across other accounts until the hot wallet was empty.', options:{ bullet:true } }
  ], { x:0.85, y:3.82, w:5.5, h:2.3, fontSize:13.5, color:TXT, fontFace:SANS,
       margin:0, valign:'top', paraSpaceAfter:7 });

  card(s, 6.9, 3.2, 5.8, 3.05);
  s.addText('What it cost', { x:7.15, y:3.38, w:5.3, h:0.35, fontSize:16, bold:true,
    color:RED, fontFace:SANS, margin:0 });
  s.addText([
    { text:'Flexcoin had no capital to absorb the loss and closed permanently two days later.\n\n',
      options:{ color:TXT } },
    { text:'Cold-storage holders were refunded after ID checks. Hot-wallet holders were not.\n\n',
      options:{ color:DIM } },
    { text:'The same week, Poloniex lost 12.3% of its bitcoin to a withdrawal race.',
      options:{ color:AMB } }
  ], { x:7.15, y:3.85, w:5.3, h:2.2, fontSize:14, fontFace:SANS, margin:0, valign:'top' });

  s.addNotes('If running long, cut this to two sentences: 896 BTC, company dead in two days.');
}

/* ========================================================================
   6 · BRIDGE
   ======================================================================== */
{
  const s = slide();
  s.addText('Neither of these was a clever attack.', { x:0.9, y:2.15, w:11.5, h:0.85,
    fontSize:38, bold:true, color:TXT, fontFace:SERIF, margin:0 });
  s.addText('Both were the ordinary read → check → write that every one of us has shipped,\nhit by more than one request at a time.',
    { x:0.92, y:3.15, w:11.3, h:1.0, fontSize:20, color:DIM, fontFace:SANS, margin:0 });

  card(s, 0.9, 4.45, 5.6, 1.35, CARD2);
  s.addText('Starbucks lost pocket change\nand some reputation.', { x:1.15, y:4.65, w:5.1,
    h:0.95, fontSize:16, color:AMB, fontFace:SANS, margin:0, valign:'middle' });

  card(s, 6.8, 4.45, 5.6, 1.35, CARD2, RED);
  s.addText('Flexcoin stopped existing.', { x:7.05, y:4.65, w:5.1, h:0.95, fontSize:16,
    bold:true, color:RED, fontFace:SANS, margin:0, valign:'middle' });

  s.addText('Same bug.', { x:0.92, y:6.05, w:11.3, h:0.5, fontSize:22, bold:true,
    color:CYAN, fontFace:SERIF, margin:0 });
}

/* ========================================================================
   7 · THE FLAWED CODE
   ======================================================================== */
{
  const s = slide();
  title(s, 'The flawed code', 'This passes code review. It has validation, an early return, and it reads clean.');

  code(s, 0.6, 1.75, 7.4, 4.4, [
    { text:'[HttpPost("withdraw")]\n', options:{ color:VIO } },
    { text:'public async Task<IResult> Withdraw(WithdrawRequest req)\n{\n', options:{ color:TXT } },
    { text:'    // READ\n', options:{ color:DIM } },
    { text:'    var wallet = await db.Wallets\n        .FirstOrDefaultAsync(w => w.Id == req.WalletId);\n\n', options:{ color:TXT } },
    { text:'    // CHECK  <-- against a stale copy in app memory\n', options:{ color:RED } },
    { text:'    if (wallet.Balance < req.Amount)\n        return Results.BadRequest("Insufficient funds");\n\n', options:{ color:TXT } },
    { text:'    // MODIFY\n', options:{ color:DIM } },
    { text:'    wallet.Balance -= req.Amount;\n\n', options:{ color:TXT } },
    { text:'    // WRITE\n', options:{ color:DIM } },
    { text:'    await db.SaveChangesAsync();\n', options:{ color:TXT } },
    { text:'    // UPDATE Wallets SET Balance=@p WHERE Id=@id\n', options:{ color:RED } },
    { text:'    //                              ^^^^^^^^^^^^ that is all\n', options:{ color:RED } },
    { text:'    return Results.Ok(wallet);\n}', options:{ color:TXT } }
  ], 12);

  card(s, 8.3, 1.75, 4.4, 4.4, CARD2);
  s.addText('Read the UPDATE literally', { x:8.55, y:1.95, w:3.9, h:0.35, fontSize:16,
    bold:true, color:AMB, fontFace:SANS, margin:0 });
  s.addText([
    { text:'"Set the balance to 900."\n\n', options:{ color:TXT, fontSize:15, bold:true } },
    { text:'Not "subtract 100".\nNot "only if it is still 1000".\n\n', options:{ color:RED, fontSize:14 } },
    { text:'An absolute value, computed in C# from a number that was true a few milliseconds ago.\n\n',
      options:{ color:DIM, fontSize:13.5 } },
    { text:'The guard on line 8 is worthless, because the value it guards is a copy — and nothing '
         + 'stops the row changing between the SELECT and the UPDATE.',
      options:{ color:DIM, fontSize:13.5 } }
  ], { x:8.55, y:2.4, w:3.9, h:3.55, fontFace:SANS, margin:0, valign:'top' });

  s.addNotes('Ask the room: what is wrong with this code? Wait for "it needs a transaction" — then go to the next slide.');
}

/* ========================================================================
   8 · THE INTERLEAVING
   ======================================================================== */
{
  const s = slide();
  title(s, 'The interleaving', 'Balance = $100. Two requests. Eight steps.');

  const rows = [
    ['t0', 'A', 'SELECT Balance', '-> 100', CYAN],
    ['t1', 'B', 'SELECT Balance', '-> 100   the same $100', VIO],
    ['t2', 'A', 'if (100 < 100)', 'false -> allowed', CYAN],
    ['t3', 'B', 'if (100 < 100)', 'false -> allowed too', VIO],
    ['t4', 'A', 'UPDATE SET Balance = 0 WHERE Id = 1', '1 row  ·  200 OK', RED],
    ['t5', 'B', 'UPDATE SET Balance = 0 WHERE Id = 1', '1 row  ·  200 OK', RED]
  ];
  let y = 1.85;
  rows.forEach(r => {
    card(s, 0.6, y, 12.1, 0.66, r[1] === 'A' ? CARD : CARD2);
    s.addText(r[0], { x:0.8, y:y+0.13, w:0.6, h:0.4, fontSize:13, color:DIM, fontFace:MONO, margin:0 });
    s.addText('Request ' + r[1], { x:1.4, y:y+0.13, w:1.4, h:0.4, fontSize:13, bold:true,
      color:r[1]==='A'?CYAN:VIO, fontFace:SANS, margin:0 });
    s.addText(r[2], { x:2.85, y:y+0.13, w:6.2, h:0.4, fontSize:13, color:TXT, fontFace:MONO, margin:0 });
    s.addText(r[3], { x:9.1, y:y+0.13, w:3.4, h:0.4, fontSize:13, color:r[4], fontFace:MONO, margin:0 });
    y += 0.74;
  });

  card(s, 0.6, 6.35, 12.1, 0.75, CARD2, RED);
  s.addText('$200 paid out of a $100 balance. Two 200 OKs. Zero exceptions. This is a LOST UPDATE — and READ COMMITTED explicitly permits it.',
    { x:0.85, y:6.45, w:11.6, h:0.55, fontSize:15, bold:true, color:RED, fontFace:SANS,
      margin:0, valign:'middle' });
  s.addNotes('Pause on t1. That is where the bug is created — not at the write.');
}

/* ========================================================================
   9 · THE FALSE FIX
   ======================================================================== */
{
  const s = slide();
  title(s, '"Just wrap it in a transaction."',
    'The most common answer in the room, and the most common false fix in code review.');

  code(s, 0.6, 1.8, 7.0, 3.1, [
    { text:'using var tx = db.Database.BeginTransaction();\n\n', options:{ color:VIO } },
    { text:'var w = await db.Wallets.FirstAsync(x => x.Id == id);\n', options:{ color:TXT } },
    { text:'if (w.Balance < amount) return Fail();\n\n', options:{ color:RED } },
    { text:'w.Balance -= amount;\nawait db.SaveChangesAsync();\ntx.Commit();\n\n', options:{ color:TXT } },
    { text:'// Both requests STILL read 100. Nothing changed.', options:{ color:RED } }
  ], 13);

  card(s, 7.9, 1.8, 4.8, 3.1, CARD2, AMB);
  s.addText('Atomicity ≠ Isolation', { x:8.15, y:2.0, w:4.3, h:0.4, fontSize:19, bold:true,
    color:AMB, fontFace:SERIF, margin:0 });
  s.addText([
    { text:'A transaction guarantees all-or-nothing. It does not isolate you from a concurrent reader.\n\n',
      options:{ color:TXT, fontSize:14 } },
    { text:'At READ COMMITTED a plain SELECT takes a shared lock and releases it the instant the statement finishes.\n\n',
      options:{ color:DIM, fontSize:13.5 } },
    { text:'Request B reads the old balance perfectly happily — inside its own transaction.',
      options:{ color:DIM, fontSize:13.5 } }
  ], { x:8.15, y:2.5, w:4.3, h:2.25, fontFace:SANS, margin:0, valign:'top' });

  s.addText('You need the check in the WHERE clause, a lock hint, or a single-statement update. Nothing else counts.',
    { x:0.6, y:5.25, w:12.1, h:0.6, fontSize:19, bold:true, color:CYAN, fontFace:SERIF, margin:0 });
  s.addNotes('This slide is the highest-value 90 seconds of the talk. Do not rush it.');
}

/* ========================================================================
   10 · OPTIMISTIC — THE ONE LINE
   ======================================================================== */
{
  const s = slide();
  title(s, 'Fix 1 — Optimistic concurrency', 'Assumption: collisions are RARE. Do not pay for a lock. Detect the collision at write time.');

  code(s, 0.6, 1.75, 12.1, 1.35, [
    { text:'// The entire fix. One line of model configuration.\n', options:{ color:DIM } },
    { text:'builder.Property(w => w.RowVersion).IsRowVersion();\n', options:{ color:GRN } },
    { text:'// or the attribute form:  [Timestamp] public byte[] RowVersion { get; set; }', options:{ color:DIM } }
  ], 14);

  card(s, 0.6, 3.3, 5.95, 1.55, CARD2, RED);
  s.addText('Before', { x:0.85, y:3.42, w:5.4, h:0.3, fontSize:12, bold:true, color:RED,
    fontFace:SANS, margin:0, charSpacing:1 });
  s.addText('UPDATE Wallets SET Balance = 0\nWHERE Id = 1;', { x:0.85, y:3.78, w:5.4, h:0.9,
    fontSize:14, color:TXT, fontFace:MONO, margin:0 });

  card(s, 6.75, 3.3, 5.95, 1.55, CARD2, GRN);
  s.addText('After', { x:7.0, y:3.42, w:5.4, h:0.3, fontSize:12, bold:true, color:GRN,
    fontFace:SANS, margin:0, charSpacing:1 });
  s.addText([
    { text:'UPDATE Wallets SET Balance = 0\nWHERE Id = 1 ', options:{ color:TXT } },
    { text:'AND RowVersion = 0x..07D1;', options:{ color:GRN, bold:true } }
  ], { x:7.0, y:3.78, w:5.4, h:0.9, fontSize:14, fontFace:MONO, margin:0 });

  s.addText('"Update this row only if it is still the version I read."',
    { x:0.6, y:5.15, w:12.1, h:0.55, fontSize:24, bold:true, color:GRN, fontFace:SERIF, margin:0 });
  s.addText('SQL Server maintains the 8-byte rowversion itself, on every UPDATE, database-wide monotonic. You never assign it. '
          + 'Row already changed → 0 rows affected → EF Core throws DbUpdateConcurrencyException.',
    { x:0.62, y:5.75, w:12.1, h:0.8, fontSize:14.5, color:DIM, fontFace:SANS, margin:0 });
  s.addNotes('Read the WHERE clause out loud. That sentence is the fix.');
}

/* ========================================================================
   11 · OPTIMISTIC — HANDLING IT
   ======================================================================== */
{
  const s = slide();
  title(s, 'It does not prevent the collision',
    'It guarantees you find out. Both requests still read stale data — the difference is that the loser is told.');

  code(s, 0.6, 1.8, 6.05, 3.55, [
    { text:'// A. Fail fast — good for edit forms\n', options:{ color:GRN } },
    { text:'try { await db.SaveChangesAsync(); }\ncatch (DbUpdateConcurrencyException ex)\n{\n', options:{ color:TXT } },
    { text:'    var entry = ex.Entries.Single();\n', options:{ color:TXT } },
    { text:'    var now = await entry\n        .GetDatabaseValuesAsync();\n\n', options:{ color:TXT } },
    { text:'    return Results.Conflict(new {\n        yours = 900, actualNow = now[..] });\n}\n\n', options:{ color:TXT } },
    { text:'// "Someone else changed this record,\n//  here is their version."', options:{ color:DIM } }
  ], 12);

  code(s, 6.9, 1.8, 5.8, 3.55, [
    { text:'// B. Retry — good for money\n', options:{ color:GRN } },
    { text:'for (var i = 1; i <= 5; i++)\n{\n', options:{ color:TXT } },
    { text:'    db.ChangeTracker.Clear();\n', options:{ color:AMB } },
    { text:'    var w = await Reload();      // RE-READ\n', options:{ color:AMB } },
    { text:'    if (w.Balance < amt)         // RE-CHECK\n        return Rejected();\n\n', options:{ color:AMB } },
    { text:'    w.Balance -= amt;\n    try { await db.SaveChangesAsync();\n          return Ok(); }\n', options:{ color:TXT } },
    { text:'    catch (DbUpdateConcurrencyException)\n    { await Task.Delay(Backoff(i)); }\n}', options:{ color:TXT } }
  ], 12);

  card(s, 0.6, 5.6, 12.1, 0.85, CARD2, RED);
  s.addText('Retry means RE-READ and RE-DECIDE. Catching the exception and calling SaveChanges again rebuilds the original bug with extra steps.',
    { x:0.85, y:5.7, w:11.6, h:0.65, fontSize:15, bold:true, color:RED, fontFace:SANS,
      margin:0, valign:'middle' });
  s.addText('Fail fast when a human is waiting and can be told. Retry when the operation is safe to recompute and a 409 would just be noise.',
    { x:0.62, y:6.6, w:12.1, h:0.4, fontSize:13.5, italic:true, color:DIM, fontFace:SANS, margin:0 });
}

/* ========================================================================
   12 · PESSIMISTIC
   ======================================================================== */
{
  const s = slide();
  title(s, 'Fix 2 — Pessimistic locking',
    'Assumption: collisions are COMMON, or a retry is unacceptable. Do not detect the collision — prevent it.');

  code(s, 0.6, 1.8, 7.3, 3.5, [
    { text:'await using var tx = await db.Database\n    .BeginTransactionAsync();\n\n', options:{ color:VIO } },
    { text:'var wallet = (await db.Wallets.FromSqlInterpolated(\n    $@"SELECT [Id], [Owner], [Balance]\n', options:{ color:TXT } },
    { text:'       FROM [Wallets] WITH (UPDLOCK, ROWLOCK)\n', options:{ color:GRN } },
    { text:'       WHERE [Id] = {id}").ToListAsync())\n    .FirstOrDefault();\n\n', options:{ color:TXT } },
    { text:'// From here until COMMIT the row is ours alone.\n', options:{ color:GRN } },
    { text:'if (wallet.Balance < amount) { ... }\nwallet.Balance -= amount;\nawait db.SaveChangesAsync();\n', options:{ color:TXT } },
    { text:'await tx.CommitAsync();  // releases the lock', options:{ color:TXT } }
  ], 11.5);

  card(s, 8.2, 1.8, 4.5, 1.65, CARD2, VIO);
  s.addText('UPDLOCK', { x:8.45, y:1.95, w:4.0, h:0.32, fontSize:15, bold:true, color:VIO, fontFace:MONO, margin:0 });
  s.addText('Take an Update lock at SELECT time instead of a Shared lock. Update locks are not compatible with each other, so a second reader blocks instead of reading stale.',
    { x:8.45, y:2.32, w:4.0, h:1.0, fontSize:12.5, color:TXT, fontFace:SANS, margin:0, valign:'top' });

  card(s, 8.2, 3.6, 4.5, 1.7, CARD2, VIO);
  s.addText('ROWLOCK / HOLDLOCK', { x:8.45, y:3.75, w:4.0, h:0.32, fontSize:15, bold:true, color:VIO, fontFace:MONO, margin:0 });
  s.addText('ROWLOCK keeps granularity at the row so unrelated wallets are untouched. Add HOLDLOCK only if you also need to block INSERTs into the range.',
    { x:8.45, y:4.12, w:4.0, h:1.05, fontSize:12.5, color:TXT, fontFace:SANS, margin:0, valign:'top' });

  card(s, 0.6, 5.5, 12.1, 1.4, CARD2, AMB);
  s.addText('Two hard requirements', { x:0.85, y:5.62, w:11.6, h:0.3, fontSize:14, bold:true,
    color:AMB, fontFace:SANS, margin:0 });
  s.addText([
    { text:'1.  The locking SELECT must be inside an explicit transaction. Without one the lock is released the instant the statement finishes — and you are back to square one.\n',
      options:{ breakLine:true } },
    { text:'2.  Nothing slow may run before the COMMIT. Every millisecond you hold the lock is a millisecond every other request on that row is stopped.' }
  ], { x:0.85, y:5.95, w:11.6, h:0.85, fontSize:13.5, color:TXT, fontFace:SANS, margin:0, valign:'top' });
}

/* ========================================================================
   13 · PESSIMISTIC — THE BILL
   ======================================================================== */
{
  const s = slide();
  title(s, 'What the lock costs you', 'Request B is not slow. Request B is stopped.');

  const lanes = [
    ['A', 'BEGIN TRAN', 'SELECT ... UPDLOCK', 'check OK', 'UPDATE', 'COMMIT - lock released', CYAN],
    ['B', 'BEGIN TRAN', 'SELECT ... UPDLOCK  [ BLOCKED ]', 'reads $0.00', '400 Insufficient funds', '', AMB]
  ];
  let y = 1.85;
  lanes.forEach(l => {
    card(s, 0.6, y, 12.1, 1.05, CARD2, l[6]);
    s.addText('Request ' + l[0], { x:0.85, y:y+0.12, w:1.6, h:0.32, fontSize:15, bold:true,
      color:l[6], fontFace:SANS, margin:0 });
    s.addText(l.slice(1,6).filter(Boolean).join('   ->   '), { x:0.85, y:y+0.5, w:11.6, h:0.42,
      fontSize:12.5, color:TXT, fontFace:MONO, margin:0, valign:'top' });
    y += 1.25;
  });

  s.addText('Request B is never given the chance to read stale data, because it is never given the chance to read.',
    { x:0.62, y:4.5, w:12.1, h:0.4, fontSize:17, color:AMB, fontFace:SERIF, margin:0 });

  chip(s, 0.6, 5.15, 3.9, 'CORRECTNESS', 'Guaranteed', GRN);
  chip(s, 4.7, 5.15, 3.9, 'THROUGHPUT ON HOT ROWS', 'Serialised', RED);
  chip(s, 8.8, 5.15, 3.9, 'MULTI-ROW RISK', 'Deadlock', RED);

  s.addText('Two rows locked in inconsistent order will deadlock — SQL Server picks a victim and raises error 1205. Always acquire locks in the same order; sorting by primary key is the cheapest total order you have.',
    { x:0.6, y:6.6, w:12.1, h:0.5, fontSize:13, color:DIM, fontFace:SANS, margin:0 });
}

/* ========================================================================
   14 · ATOMIC
   ======================================================================== */
{
  const s = slide();
  title(s, 'Before either of those — do you need the read at all?',
    'If the operation fits in one statement, there is no window to race in.');

  code(s, 0.6, 1.8, 12.1, 1.5, [
    { text:'UPDATE Wallets\nSET   Balance = ', options:{ color:TXT } },
    { text:'Balance - @amount', options:{ color:GRN, bold:true } },
    { text:'\nWHERE Id = @id ', options:{ color:TXT } },
    { text:'AND Balance >= @amount', options:{ color:GRN, bold:true } },
    { text:';', options:{ color:TXT } }
  ], 16);

  card(s, 0.6, 3.5, 5.95, 1.6, CARD2, GRN);
  s.addText('Balance - @amount is RELATIVE', { x:0.85, y:3.65, w:5.45, h:0.32, fontSize:14.5,
    bold:true, color:GRN, fontFace:SANS, margin:0 });
  s.addText('No stale number ever leaves your process. The old code sent "set it to 900" — a value computed in C# from a stale read. This says "subtract 100 from whatever is there."',
    { x:0.85, y:4.02, w:5.45, h:0.95, fontSize:13, color:TXT, fontFace:SANS, margin:0, valign:'top' });

  card(s, 6.75, 3.5, 5.95, 1.6, CARD2, GRN);
  s.addText('The rule is IN the statement', { x:7.0, y:3.65, w:5.45, h:0.32, fontSize:14.5,
    bold:true, color:GRN, fontFace:SANS, margin:0 });
  s.addText('rowsAffected == 0 means "rejected, insufficient funds" — and that answer is authoritative, not a guess based on a value read 40 ms ago.',
    { x:7.0, y:4.02, w:5.45, h:0.95, fontSize:13, color:TXT, fontFace:SANS, margin:0, valign:'top' });

  code(s, 0.6, 5.3, 12.1, 1.25, [
    { text:'// EF Core 7+\n', options:{ color:DIM } },
    { text:'var rows = await db.Wallets\n    .Where(w => w.Id == id && w.Balance >= amount)\n    .ExecuteUpdateAsync(s => s.SetProperty(w => w.Balance, w => w.Balance - amount));',
      options:{ color:TXT } }
  ], 12.5);

  foot(s, 'What you give up: ExecuteUpdate bypasses the change tracker — no navigation fix-up, no SaveChanges interceptors, no domain events.');
}

/* ========================================================================
   15 · DECIDE
   ======================================================================== */
{
  const s = slide();
  title(s, 'Which one do I use?', 'Ask these three questions, in this order.');

  const opts = [
    ['1', 'Atomic UPDATE', 'Can I express it as one statement whose WHERE clause carries the business rule?',
     'Cheapest of all — one round trip, no retry, no lock across your C#. Balances, counters, stock, quotas.', GRN],
    ['2', 'Optimistic — RowVersion', 'If not: is a retry acceptable for this operation?',
     'Free until a collision happens. Human-paced edits, admin screens, ordinary CRUD. Degrades as contention rises.', CYAN],
    ['3', 'Pessimistic — UPDLOCK', 'If not: take the lock, keep the transaction short, measure the cost.',
     'Contention is high or retrying is illegal — charging a card, moving money, allocating the last seat.', VIO]
  ];
  let y = 1.85;
  opts.forEach(o => {
    card(s, 0.6, y, 12.1, 1.55, CARD, o[4]);
    dot(s, 0.85, y+0.28, o[0], o[4]);
    s.addText(o[1], { x:1.45, y:y+0.16, w:4.2, h:0.4, fontSize:17, bold:true, color:o[4],
      fontFace:SANS, margin:0, valign:'middle' });
    s.addText(o[2], { x:1.45, y:y+0.6, w:11.0, h:0.35, fontSize:13.5, color:TXT,
      fontFace:SANS, margin:0, valign:'top' });
    s.addText(o[3], { x:1.45, y:y+0.98, w:11.0, h:0.4, fontSize:12.5, color:DIM,
      fontFace:SANS, margin:0, valign:'top' });
    y += 1.72;
  });

  s.addText('If question 1 fits your operation, stop there. Most balance, counter, stock and quota updates do.',
    { x:0.6, y:7.05, w:12.1, h:0.35, fontSize:13.5, italic:true, color:AMB, fontFace:SANS, margin:0 });
}

/* ========================================================================
   16 · NOT FIXES
   ======================================================================== */
{
  const s = slide();
  title(s, 'Looks like a fix. Isn\'t.', 'The five you will meet in code review.');

  const bad = [
    ['BeginTransaction() with no lock hint', 'Atomicity, not isolation. Both requests still read the old value. The single most common false fix.'],
    ['C# lock / SemaphoreSlim', 'Works on one process. The day you run two pods it silently stops working — and silence is the worst property a concurrency control can have.'],
    ['Retrying without re-reading', 'Catching DbUpdateConcurrencyException and calling SaveChanges again rebuilds the original bug with extra steps.'],
    ['[ConcurrencyCheck] on one column', 'Fine until the operation touches a second field. rowversion covers the whole row and the database maintains it for you.'],
    ['Serializable everywhere', 'It does work — and it will flood you with deadlocks. A deliberate choice for one operation, never a global default.']
  ];
  let y = 1.8;
  bad.forEach(bd => {
    card(s, 0.6, y, 12.1, 0.95, CARD2, RED);
    s.addText('✕', { x:0.85, y:y+0.1, w:0.4, h:0.4, fontSize:17, bold:true, color:RED,
      fontFace:SANS, margin:0, align:'center' });
    s.addText(bd[0], { x:1.3, y:y+0.1, w:4.5, h:0.35, fontSize:14, bold:true, color:TXT,
      fontFace:SANS, margin:0, valign:'middle' });
    s.addText(bd[1], { x:1.3, y:y+0.45, w:11.2, h:0.42, fontSize:12.5, color:DIM,
      fontFace:SANS, margin:0, valign:'top' });
    y += 1.06;
  });

  s.addText('And: "it has never happened in production." A lost update writes no log. Absence of alerts is not evidence of absence.',
    { x:0.6, y:7.05, w:12.1, h:0.35, fontSize:13.5, italic:true, color:AMB, fontFace:SANS, margin:0 });
}

/* ========================================================================
   17 · CLOSE
   ======================================================================== */
{
  const s = slide();
  s.addText('Take this to your next code review.', { x:0.8, y:0.65, w:11.7, h:0.7,
    fontSize:32, bold:true, color:TXT, fontFace:SERIF, margin:0 });

  code(s, 0.8, 1.6, 11.7, 3.9, [
    { text:'Concurrency review — ask these five:\n\n', options:{ color:CYAN, bold:true } },
    { text:'1.  Does this method read a row, decide something, then write that row?\n\n', options:{ color:TXT } },
    { text:'2.  Can two requests hit it at the same time?\n    (Retry policies and double-clicks count. So does at-least-once delivery.)\n\n', options:{ color:TXT } },
    { text:'3.  Is the business rule in the WHERE clause, or only in C#?\n\n', options:{ color:TXT } },
    { text:'4.  Does the entity have a concurrency token — and does this path use it?\n\n', options:{ color:TXT } },
    { text:'5.  If it uses a lock: is it inside a transaction, and is the transaction short?\n\n', options:{ color:TXT } },
    { text:'If 1 and 2 are yes and 3, 4 and 5 are all no — you have a lost update.\nIt will not throw. It will not log. Fix it now.', options:{ color:RED, bold:true } }
  ], 14);

  s.addText('When you see read → check → write on data more than one request can touch, stop and ask:\nwhat happens if two of these run at once? If the answer is not in the WHERE clause, it is not handled.',
    { x:0.82, y:5.75, w:11.6, h:0.95, fontSize:17, color:CYAN, fontFace:SERIF, margin:0 });
  s.addNotes('Paste the checklist in the team channel afterwards.');
}

pres.writeFile({ fileName: process.argv[2] || 'race-conditions-ef-core.pptx' })
  .then(f => console.log('written:', f));
