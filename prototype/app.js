'use strict';
const M = Market;
const key = 'moneytown-v1';
const element = id => document.getElementById(id);
const money = value => `${value.toLocaleString('ko-KR')}원`;
let state = M.create();
let selected = 0;
let blocked = false;
try {
  const saved = localStorage.getItem(key);
  if (saved !== null) state = M.restore(saved);
} catch (error) {
  blocked = true;
  element('message').textContent = `저장을 읽지 못했습니다. ${error.message} 초기화하거나 브라우저 저장 설정을 확인하세요.`;
}
function save() {
  try { localStorage.setItem(key, JSON.stringify(state)); return true; }
  catch { element('message').textContent = '이번 행동은 반영됐지만 저장하지 못했습니다. 브라우저 저장 설정을 확인하세요. 새로고침하면 진행을 잃을 수 있습니다.'; return false; }
}
function act(action, message) {
  if (blocked) return;
  try { action(); element('message').textContent = message; render(); save(); }
  catch (error) { element('message').textContent = error.message; }
}
function estimate() {
  try {
    const order = M.quote(state, selected, Number(element('quantity').value));
    element('estimate').textContent = `매수 지출 ${money(order.buy)} · 매도 수입 ${money(order.sell)} · 수수료 각각 ${money(order.fee)}`;
  } catch (error) { element('estimate').textContent = error.message; }
}
function drawChart(stock) {
  const values = stock.history;
  const low = Math.min(...values), high = Math.max(...values);
  const span = high - low || 1;
  const points = values.map((price, i) => `${20 + i * 320 / Math.max(1, values.length - 1)},${130 - (price - low) * 100 / span}`);
  const svg = element('chart');
  svg.replaceChildren();
  const add = (tag, attrs, text) => {
    const node = document.createElementNS('http://www.w3.org/2000/svg', tag);
    Object.entries(attrs).forEach(([key, value]) => node.setAttribute(key, value));
    if (text) node.textContent = text;
    svg.append(node);
  };
  [30, 80, 130].forEach(y => add('line', { x1: 20, x2: 340, y1: y, y2: y, stroke: '#274052', 'stroke-dasharray': '4 4' }));
  add('polyline', { points: points.join(' '), fill: 'none', stroke: '#5bdccb', 'stroke-width': 3 });
  const [x, y] = points.at(-1).split(',');
  add('circle', { cx: x, cy: y, r: 4, fill: '#5bdccb' });
  add('text', { x: 20, y: 18, fill: '#a5b7ca', 'font-size': 11 }, `최고 ${money(high)} / 최저 ${money(low)}`);
  svg.setAttribute('aria-label', `${M.companies[selected].name} 최근 ${values.length}개 거래일, 최저 ${money(low)}, 최고 ${money(high)}, 현재 ${money(stock.price)}`);
}
function render() {
  const company = M.companies[selected], stock = state.stocks[selected];
  element('cash').textContent = money(state.cash);
  element('total').textContent = money(M.total(state));
  element('day').textContent = `${state.day}일차`;
  element('name').textContent = company.name;
  element('sector').textContent = `${company.sector} · 가상 기업`;
  element('price').textContent = money(stock.price);
  const previous = stock.history.at(-2) || stock.price;
  const change = (stock.price / previous - 1) * 100;
  element('change').textContent = `전일 대비 ${change > 0 ? '+' : ''}${change.toFixed(2)}%`;
  element('change').className = change > 0 ? 'up' : change < 0 ? 'down' : '';
  element('holding').textContent = `보유 ${stock.quantity}주 · 평균 원가 ${money(stock.quantity ? Math.round(stock.cost / stock.quantity) : 0)}`;
  element('profit').textContent = `평가손익 ${money(stock.price * stock.quantity - stock.cost)} · 전체 실현손익 ${money(state.realized)} (평가손익은 매도 수수료 제외)`;
  element('news').textContent = `${state.news.day}일차 · ${state.news.title}`;
  const valid = state.reports[selected] >= state.day;
  element('report').textContent = valid
    ? `${company.name}: 장기 성장 성향 ${company.growth === 0.002 ? '높음' : '보통'}, 가격 변동성 ${company.volatility >= 0.035 ? '높음' : '보통'}. 뉴스와 반대 방향으로 움직일 수 있습니다. ${state.reports[selected]}일차까지 유효.`
    : `${company.name}의 성장 성향과 변동성을 확인하세요.${state.reports[selected] ? ' 이전 보고서는 만료되었습니다.' : ''}`;
  element('report-buy').disabled = blocked || valid;
  element('deposit').textContent = state.deposit ? `${money(state.deposit.principal)} 예치 중 · ${state.deposit.due}일차에 100,500원 반환` : '3일 동안 자금을 맡겨 정해진 보상을 받습니다.';
  element('deposit-buy').disabled = blocked || !!state.deposit;
  ['buy', 'sell', 'next'].forEach(id => { element(id).disabled = blocked; });
  Array.from(element('companies').children).forEach((button, i) => button.setAttribute('aria-pressed', i === selected));
  drawChart(stock);
  estimate();
}
M.companies.forEach((company, i) => {
  const button = document.createElement('button');
  button.textContent = company.name;
  button.onclick = () => { selected = i; render(); };
  element('companies').append(button);
});
['buy', 'sell'].forEach(side => {
  element(side).onclick = () => act(() => M.trade(state, selected, Number(element('quantity').value), side), `${M.companies[selected].name} ${side === 'buy' ? '매수' : '매도'} 완료.`);
});
element('quantity').oninput = estimate;
element('next').onclick = () => act(() => M.nextDay(state), '하루가 지났습니다. 가격과 새 뉴스를 확인하세요.');
element('report-buy').onclick = () => act(() => M.buyReport(state, selected), '기업 분석 보고서를 구매했습니다.');
element('deposit-buy').onclick = () => act(() => M.deposit(state), '예금에 가입했습니다. 3일 후 자동 반환됩니다.');
element('reset').onclick = () => {
  if (!confirm('현재 진행과 저장을 지우고 처음부터 시작할까요?')) return;
  try { localStorage.removeItem(key); }
  catch { element('message').textContent = '저장을 초기화하지 못했습니다. 브라우저 저장 설정을 확인하세요.'; return; }
  state = M.create(); blocked = false; selected = 0;
  element('message').textContent = '초기 자금 1,000,000원으로 다시 시작합니다.';
  render(); save();
};
render();
