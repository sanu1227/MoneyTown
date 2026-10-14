package com.moneytown.game

import org.junit.Assert.*
import org.junit.Test

class MarketTest {
    @Test fun feesAndPartialSalePreserveCost() {
        var s = Market.trade(Market.create(), 0, 3, true)
        assertEquals(969970, s.cash)
        s = Market.trade(s, 0, 1, false)
        assertEquals(-20, s.realized); assertEquals(20020, s.stocks[0].cost)
        s = Market.trade(s, 0, 2, false)
        assertEquals(999940, s.cash); assertEquals(-60, s.realized)
        assertEquals(0, s.stocks[0].cost); assertEquals(0, s.stocks[0].quantity)
    }
    @Test fun invalidTradesDoNotChangeInput() {
        val s = Market.create()
        for (n in listOf(0L, -1L, 100L, Long.MAX_VALUE)) {
            assertThrows(Exception::class.java) { Market.trade(s, 0, n, true) }
        }
        assertThrows(Exception::class.java) { Market.trade(s, 0, 1, false) }
        assertThrows(Exception::class.java) { Market.trade(s, -1, 1, true) }
        assertEquals(Market.create(), s)
    }
    @Test fun reportExpiresAndCannotBePurchasedTwice() {
        var s = Market.buyReport(Market.create(), 0)
        assertEquals(998000, s.cash); assertEquals(3, s.reports[0])
        assertThrows(Exception::class.java) { Market.buyReport(s, 0) }
        repeat(3) { s = Market.nextDay(s) }
        s = Market.buyReport(s, 0)
        assertEquals(6, s.reports[0])
        assertThrows(Exception::class.java) { Market.buyReport(s.copy(cash = 0), 1) }
    }
    @Test fun depositMaturesExactlyOnce() {
        var s = Market.deposit(Market.create())
        assertEquals(1000000, Market.total(s)); assertEquals(900000, s.cash)
        assertThrows(Exception::class.java) { Market.deposit(s) }
        repeat(2) { s = Market.nextDay(s) }; assertEquals(900000, s.cash)
        s = Market.nextDay(s); assertEquals(1000500, s.cash); assertNull(s.deposit)
        s = Market.nextDay(s); assertEquals(1000500, s.cash)
    }
    @Test fun saveRoundTripPreservesFutureMarket() {
        val s = Market.nextDay(Market.deposit(Market.buyReport(Market.trade(Market.create(), 2, 2, true), 1)))
        val restored = SaveCodec.decode(SaveCodec.encode(s))
        assertEquals(s, restored)
        assertEquals(Market.nextDay(s), Market.nextDay(restored))
    }
    @Test fun corruptedAndUnsupportedSavesAreRejected() {
        val bytes = SaveCodec.encode(Market.create())
        assertThrows(Exception::class.java) { SaveCodec.decode(bytes.copyOf(20)) }
        assertThrows(Exception::class.java) { SaveCodec.decode(bytes + byteArrayOf(0)) }
        bytes[7] = 99
        assertThrows(Exception::class.java) { SaveCodec.decode(bytes) }
        assertThrows(Exception::class.java) { SaveCodec.encode(Market.create().copy(cash = -1)) }
        assertThrows(Exception::class.java) { SaveCodec.encode(Market.create().copy(reports = listOf(0))) }
    }
    @Test fun deterministicMarketMatchesWebBaseline() {
        val s = Market.nextDay(Market.create())
        assertEquals(10008, s.stocks[0].price)
        assertEquals(5, s.news.company); assertTrue(s.news.positive)
        var a = Market.create(42); var b = Market.trade(a, 0, 1, true)
        repeat(100) { a = Market.nextDay(a); b = Market.nextDay(b) }
        assertEquals(a.stocks.map { it.history }, b.stocks.map { it.history })
        assertEquals(a.news, b.news)
        assertTrue(a.stocks.all { it.price >= 100 && it.history.size == 30 })
    }
    @Test fun overflowAndFractionalCostAreHandled() {
        val s = Market.create()
        assertThrows(Exception::class.java) { Market.quote(s, 0, Market.LIMIT) }
        val stocks = s.stocks.toMutableList()
        stocks[0] = stocks[0].copy(quantity = 3, cost = 10001)
        var changed = Market.trade(s.copy(stocks = stocks), 0, 1, false)
        assertEquals(6668, changed.stocks[0].cost)
        changed = Market.trade(changed, 0, 2, false)
        assertEquals(0, changed.stocks[0].cost)
    }
}
