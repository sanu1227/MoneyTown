/* 브라우저와 Node 테스트에서 같은 거래 규칙을 사용합니다. */
(function (root) {
  'use strict';
  const companies = [
    { name: '그린푸드', sector: '식품', price: 10000, growth: 0.001, volatility: 0.018 },
    { name: '픽셀게임즈', sector: '게임', price: 16000, growth: 0.002, volatility: 0.045 },
    { name: '노바테크', sector: '기술', price: 24000, growth: 0.002, volatility: 0.04 },
    { name: '솔라에너지', sector: '에너지', price: 12000, growth: 0.001, volatility: 0.03 },
    { name: '브릿지물류', sector: '물류', price: 8000, growth: 0.001, volatility: 0.02 },
    { name: '온유메디컬', sector: '의료', price: 20000, growth: 0.001, volatility: 0.035 }
  ];
  function random(state) {
    state.seed = (Math.imul(state.seed, 1664525) + 1013904223) >>> 0;
    return state.seed / 4294967296;
  }
  function makeNews(state) {
    const company = Math.floor(random(state) * companies.length);
    const positive = random(state) > 0.5;
    return {
      day: state.day, company, effect: positive ? 0.025 : -0.025,
      title: `${companies[company].name}, ${positive ? '수요 증가로 실적 기대 상승' : '비용 증가로 수익성 우려'}`
    };
  }
  function create(seed = 20261002) {
    const state = {
      version: 1, day: 1, seed: seed >>> 0, cash: 1000000, realized: 0,
      stocks: companies.map(c => ({ price: c.price, history: [c.price], quantity: 0, cost: 0 })),
      reports: companies.map(() => 0), deposit: null, news: null
    };
    state.news = makeNews(state);
    return state;
  }
  function safeInteger(value, minimum = 0) {
    return Number.isSafeInteger(value) && value >= minimum;
  }
  function stockAt(state, index) {
    if (!safeInteger(index) || index >= companies.length) throw Error('종목을 확인하세요.');
    return state.stocks[index];
  }
  function quote(state, index, quantity) {
    const stock = stockAt(state, index);
    if (!safeInteger(quantity, 1)) throw Error('수량은 1 이상의 정수로 입력하세요.');
    const gross = stock.price * quantity;
    const fee = Math.ceil(gross / 1000);
    if (!safeInteger(gross) || !safeInteger(gross + fee)) throw Error('거래 수량이 너무 큽니다.');
    return { gross, fee, buy: gross + fee, sell: gross - fee };
  }
  function trade(state, index, quantity, side) {
    const stock = stockAt(state, index);
    const order = quote(state, index, quantity);
    if (side === 'buy') {
      if (state.cash < order.buy) throw Error('보유 현금이 부족합니다.');
      if (!safeInteger(stock.cost + order.buy) || !safeInteger(stock.quantity + quantity)) throw Error('보유 한도를 초과합니다.');
      state.cash -= order.buy;
      stock.cost += order.buy;
      stock.quantity += quantity;
    } else if (side === 'sell') {
      if (stock.quantity < quantity) throw Error('보유 주식이 부족합니다.');
      if (!safeInteger(state.cash + order.sell)) throw Error('현금 한도를 초과합니다.');
      const cost = Number(BigInt(stock.cost) * BigInt(quantity) / BigInt(stock.quantity));
      const realized = state.realized + order.sell - cost;
      if (!Number.isSafeInteger(realized)) throw Error('손익 한도를 초과합니다.');
      state.cash += order.sell;
      state.realized = realized;
      stock.cost -= cost;
      stock.quantity -= quantity;
    } else throw Error('거래 종류를 확인하세요.');
    return order;
  }
  function buyReport(state, index) {
    stockAt(state, index);
    if (state.reports[index] >= state.day) throw Error('이미 유효한 보고서를 보유하고 있습니다.');
    if (state.cash < 2000) throw Error('보고서 구매 비용이 부족합니다.');
    state.cash -= 2000;
    state.reports[index] = state.day + 2;
  }
  function deposit(state) {
    if (state.deposit) throw Error('진행 중인 예금이 있습니다.');
    if (state.cash < 100000) throw Error('예금할 현금이 부족합니다.');
    state.cash -= 100000;
    state.deposit = { principal: 100000, due: state.day + 3 };
  }
  function nextDay(state) {
    // 다음 상태를 먼저 계산해 오류 발생 시 현재 상태를 유지합니다.
    const next = structuredClone(state);
    next.stocks.forEach((stock, i) => {
      const c = companies[i];
      const effect = next.news.company === i ? next.news.effect : 0;
      const change = Math.max(-0.12, Math.min(0.12, c.growth + effect + (random(next) * 2 - 1) * c.volatility));
      stock.price = Math.max(100, Math.round(stock.price * (1 + change)));
      if (!safeInteger(stock.price, 100) || !safeInteger(stock.price * stock.quantity)) throw Error('자산 한도를 초과합니다.');
      stock.history = [...stock.history, stock.price].slice(-30);
    });
    next.day++;
    if (!safeInteger(next.day, 1)) throw Error('날짜 한도를 초과합니다.');
    if (next.deposit && next.day >= next.deposit.due) {
      next.cash += next.deposit.principal + 500;
      next.deposit = null;
    }
    next.news = makeNews(next);
    if (!safeInteger(total(next))) throw Error('총자산 한도를 초과합니다.');
    Object.assign(state, next);
  }
  function total(state) {
    return state.cash + state.stocks.reduce((sum, stock) => sum + stock.price * stock.quantity, 0) + (state.deposit?.principal || 0);
  }
  function restore(text) {
    const state = JSON.parse(text);
    const fail = () => { throw Error('저장 데이터가 손상되었거나 지원하지 않는 버전입니다. 기존 저장은 보존됩니다.'); };
    if (!state || state.version !== 1 || !safeInteger(state.day, 1) || !safeInteger(state.seed) || state.seed > 4294967295 || !safeInteger(state.cash) || !Number.isSafeInteger(state.realized)) fail();
    if (!Array.isArray(state.stocks) || state.stocks.length !== companies.length) fail();
    state.stocks.forEach(stock => {
      if (!stock || !safeInteger(stock.price, 100) || !safeInteger(stock.quantity) || !safeInteger(stock.cost) || (stock.quantity === 0 && stock.cost !== 0)) fail();
      if (!Array.isArray(stock.history) || stock.history.length < 1 || stock.history.length > 30 || !stock.history.every(price => safeInteger(price, 100)) || stock.history.at(-1) !== stock.price || !safeInteger(stock.price * stock.quantity)) fail();
    });
    if (!Array.isArray(state.reports) || state.reports.length !== companies.length || !state.reports.every(day => safeInteger(day) && day <= state.day + 2)) fail();
    if (state.deposit !== null && (!state.deposit || state.deposit.principal !== 100000 || !safeInteger(state.deposit.due) || state.deposit.due <= state.day || state.deposit.due > state.day + 3)) fail();
    if (!state.news || state.news.day !== state.day || !safeInteger(state.news.company) || state.news.company >= companies.length || typeof state.news.title !== 'string' || state.news.title.length > 200 || ![0.025, -0.025].includes(state.news.effect)) fail();
    if (!safeInteger(total(state))) fail();
    return state;
  }
  const api = { companies, create, quote, trade, buyReport, deposit, nextDay, total, restore };
  if (typeof module !== 'undefined' && module.exports) module.exports = api;
  else root.Market = api;
})(globalThis);
