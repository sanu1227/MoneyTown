const test = require('node:test');
const assert = require('node:assert/strict');
const M = require('../prototype/market.js');

test('매수 및 부분·전량 매도 후 원가와 수수료가 보존된다', () => {
  const s = M.create();
  M.trade(s, 0, 3, 'buy');
  assert.equal(s.cash, 969970);
  assert.equal(s.stocks[0].cost, 30030);
  M.trade(s, 0, 1, 'sell');
  assert.equal(s.realized, -20);
  assert.equal(s.stocks[0].cost, 20020);
  M.trade(s, 0, 2, 'sell');
  assert.equal(s.cash, 999940);
  assert.equal(s.realized, -60);
  assert.equal(s.stocks[0].quantity, 0);
  assert.equal(s.stocks[0].cost, 0);
});
test('잘못된 거래는 상태를 변경하지 않는다', () => {
  const s = M.create(), before = JSON.stringify(s);
  for (const quantity of [0, -1, 1.5, NaN, Infinity, Number.MAX_SAFE_INTEGER]) {
    assert.throws(() => M.trade(s, 0, quantity, 'buy'));
  }
  assert.throws(() => M.trade(s, 0, 100, 'buy'));
  assert.throws(() => M.trade(s, 0, 1, 'sell'));
  assert.throws(() => M.trade(s, -1, 1, 'buy'));
  assert.throws(() => M.trade(s, 0, 1, 'unknown'));
  assert.equal(JSON.stringify(s), before);
});
test('보고서 중복 구매 차단과 만료 후 재구매', () => {
  const s = M.create(); M.buyReport(s, 0);
  assert.equal(s.cash, 998000);
  assert.equal(s.reports[0], 3);
  assert.throws(() => M.buyReport(s, 0));
  for (let i = 0; i < 3; i++) M.nextDay(s);
  M.buyReport(s, 0); assert.equal(s.reports[0], 6);
  s.cash = 0; assert.throws(() => M.buyReport(s, 1));
});
test('예금 원금은 총자산에 포함되며 만기 보상은 한 번만 지급된다', () => {
  const s = M.create(); M.deposit(s);
  assert.equal(M.total(s), 1000000);
  assert.equal(s.cash, 900000); assert.throws(() => M.deposit(s));
  M.nextDay(s); M.nextDay(s); assert.equal(s.cash, 900000);
  M.nextDay(s); assert.equal(s.cash, 1000500); assert.equal(s.deposit, null);
  M.nextDay(s); assert.equal(s.cash, 1000500);
  s.cash = 0; assert.throws(() => M.deposit(s));
});
test('저장 복원은 시세 재현을 유지하고 손상 데이터는 거절한다', () => {
  const s = M.create(); M.trade(s, 2, 2, 'buy'); M.buyReport(s, 3); M.deposit(s);
  M.nextDay(s);
  const restored = M.restore(JSON.stringify(s));
  assert.deepEqual(restored, s); M.nextDay(s); M.nextDay(restored); assert.deepEqual(restored, s);
  for (const mutate of [s => s.cash = -1, s => s.stocks[0].quantity = 0.5, s => s.stocks[0].history = [], s => s.version = 2, s => s.news.effect = 1, s => s.deposit.due = s.day, s => s.reports = []]) {
    const invalid = M.create(); M.deposit(invalid); mutate(invalid);
    assert.throws(() => M.restore(JSON.stringify(invalid)));
  }
  assert.throws(() => M.restore('{'));
});
test('동일 시드는 같은 시장이며 거래는 시장 난수에 영향을 주지 않는다', () => {
  const a = M.create(42), b = M.create(42); M.trade(a, 0, 1, 'buy');
  for (let day = 0; day < 100; day++) { M.nextDay(a); M.nextDay(b); }
  assert.deepEqual(a.stocks.map(s => s.price), b.stocks.map(s => s.price));
  assert.deepEqual(a.news, b.news);
  assert.ok(a.stocks.every(s => s.price >= 100 && s.history.length === 30));
});
