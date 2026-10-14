package com.moneytown.game

import java.math.BigInteger
import kotlin.math.floor

data class Company(val name: String, val sector: String, val initialPrice: Long, val growth: Double, val volatility: Double)
data class Stock(val price: Long, val history: List<Long>, val quantity: Long = 0, val cost: Long = 0)
data class News(val day: Long, val company: Int, val positive: Boolean) {
    val title: String get() = "${Market.companies[company].name}, " +
        if (positive) "수요 증가로 실적 기대 상승" else "비용 증가로 수익성 우려"
}
data class Deposit(val due: Long)
data class GameState(
    val day: Long, val seed: Long, val cash: Long, val realized: Long,
    val stocks: List<Stock>, val reports: List<Long>, val deposit: Deposit?, val news: News
)
data class Quote(val gross: Long, val fee: Long) {
    val buy get() = gross + fee
    val sell get() = gross - fee
}

object Market {
    // 웹 기준과 비교 가능한 정수 범위를 유지합니다.
    const val LIMIT = 9_007_199_254_740_991L
    val companies = listOf(
        Company("그린푸드", "식품", 10000, .001, .018),
        Company("픽셀게임즈", "게임", 16000, .002, .045),
        Company("노바테크", "기술", 24000, .002, .04),
        Company("솔라에너지", "에너지", 12000, .001, .03),
        Company("브릿지물류", "물류", 8000, .001, .02),
        Company("온유메디컬", "의료", 20000, .001, .035)
    )
    private class Random(var seed: Long) {
        fun next(): Double {
            seed = (seed * 1664525 + 1013904223) and 0xffffffffL
            return seed / 4294967296.0
        }
        fun news(day: Long) = News(day, (next() * companies.size).toInt(), next() > .5)
    }
    fun create(seed: Long = 20261002): GameState {
        val random = Random(seed and 0xffffffffL)
        val news = random.news(1)
        return GameState(1, random.seed, 1000000, 0,
            companies.map { Stock(it.initialPrice, listOf(it.initialPrice)) },
            List(companies.size) { 0 }, null, news)
    }
    private fun amount(value: Long): Long {
        require(value in 0..LIMIT) { "금액 한도를 초과했습니다." }
        return value
    }
    private fun add(a: Long, b: Long) = amount(Math.addExact(a, b))
    private fun multiply(a: Long, b: Long) = amount(Math.multiplyExact(a, b))
    fun total(s: GameState): Long = s.stocks.fold(add(s.cash, if (s.deposit != null) 100000 else 0)) { sum, stock ->
        add(sum, multiply(stock.price, stock.quantity))
    }
    fun validate(s: GameState): GameState {
        require(s.day in 1..LIMIT - 3 && s.seed in 0..0xffffffffL)
        amount(s.cash)
        require(s.realized in -LIMIT..LIMIT)
        require(s.stocks.size == companies.size && s.reports.size == companies.size)
        s.stocks.forEach {
            require(it.price in 100..LIMIT && it.quantity in 0..LIMIT && it.cost in 0..LIMIT)
            require(it.quantity != 0L || it.cost == 0L)
            require(it.history.size in 1..30 && it.history.last() == it.price)
            require(it.history.all { price -> price in 100..LIMIT })
        }
        require(s.reports.all { it in 0..s.day + 2 })
        require(s.deposit == null || s.deposit.due in s.day + 1..s.day + 3)
        require(s.news.day == s.day && s.news.company in companies.indices)
        total(s)
        return s
    }
    fun quote(s: GameState, index: Int, quantity: Long): Quote {
        require(index in companies.indices) { "종목을 확인하세요." }
        require(quantity in 1..LIMIT) { "수량은 1 이상의 정수로 입력하세요." }
        val gross = multiply(s.stocks[index].price, quantity)
        val fee = gross / 1000 + if (gross % 1000 == 0L) 0 else 1
        add(gross, fee)
        return Quote(gross, fee)
    }
    fun trade(s: GameState, index: Int, quantity: Long, buy: Boolean): GameState {
        val order = quote(s, index, quantity)
        val stock = s.stocks[index]
        val changed: Stock
        val cash: Long
        var realized = s.realized
        if (buy) {
            require(s.cash >= order.buy) { "보유 현금이 부족합니다." }
            cash = s.cash - order.buy
            changed = stock.copy(quantity = add(stock.quantity, quantity), cost = add(stock.cost, order.buy))
        } else {
            require(stock.quantity >= quantity) { "보유 주식이 부족합니다." }
            // quantity <= stock.quantity이므로 결과는 검증된 기존 원가 이하입니다.
            val cost = (BigInteger.valueOf(stock.cost) * BigInteger.valueOf(quantity) /
                BigInteger.valueOf(stock.quantity)).toLong()
            cash = add(s.cash, order.sell)
            realized = Math.addExact(s.realized, order.sell - cost)
            changed = stock.copy(quantity = stock.quantity - quantity, cost = stock.cost - cost)
        }
        return validate(s.copy(cash = cash, realized = realized,
            stocks = s.stocks.mapIndexed { i, old -> if (i == index) changed else old }))
    }
    fun buyReport(s: GameState, index: Int): GameState {
        require(index in companies.indices) { "종목을 확인하세요." }
        require(s.reports[index] < s.day) { "유효한 보고서를 이미 보유하고 있습니다." }
        require(s.cash >= 2000) { "보고서 구매 비용이 부족합니다." }
        return validate(s.copy(cash = s.cash - 2000,
            reports = s.reports.mapIndexed { i, day -> if (i == index) s.day + 2 else day }))
    }
    fun deposit(s: GameState): GameState {
        require(s.deposit == null) { "이미 진행 중인 예금이 있습니다." }
        require(s.cash >= 100000) { "예금할 현금이 부족합니다." }
        return validate(s.copy(cash = s.cash - 100000, deposit = Deposit(s.day + 3)))
    }
    fun nextDay(s: GameState): GameState {
        val random = Random(s.seed)
        val stocks = s.stocks.mapIndexed { i, stock ->
            val c = companies[i]
            val effect = if (s.news.company != i) 0.0 else if (s.news.positive) .025 else -.025
            val change = (c.growth + effect + (random.next() * 2 - 1) * c.volatility).coerceIn(-.12, .12)
            val price = floor(stock.price * (1 + change) + .5).toLong().coerceAtLeast(100)
            stock.copy(price = price, history = (stock.history + price).takeLast(30))
        }
        val day = s.day + 1
        val matured = s.deposit != null && day >= s.deposit.due
        val news = random.news(day)
        return validate(s.copy(day = day, seed = random.seed, stocks = stocks, news = news,
            cash = if (matured) add(s.cash, 100500) else s.cash,
            deposit = if (matured) null else s.deposit))
    }
}
