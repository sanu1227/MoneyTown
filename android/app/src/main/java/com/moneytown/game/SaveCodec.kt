package com.moneytown.game

import java.io.ByteArrayInputStream
import java.io.ByteArrayOutputStream
import java.io.DataInputStream
import java.io.DataOutputStream

/** 작은 버전 지정 바이너리 저장 형식. 외부 객체 역직렬화를 사용하지 않습니다. */
object SaveCodec {
    private const val MAGIC = 0x4d544f57
    fun encode(state: GameState): ByteArray {
        Market.validate(state)
        val bytes = ByteArrayOutputStream()
        DataOutputStream(bytes).use { out ->
            out.writeInt(MAGIC); out.writeInt(1)
            out.writeLong(state.day); out.writeLong(state.seed)
            out.writeLong(state.cash); out.writeLong(state.realized)
            state.stocks.forEach { stock ->
                out.writeLong(stock.price); out.writeLong(stock.quantity); out.writeLong(stock.cost)
                out.writeInt(stock.history.size)
                stock.history.forEach(out::writeLong)
            }
            state.reports.forEach(out::writeLong)
            out.writeLong(state.deposit?.due ?: 0)
            out.writeInt(state.news.company); out.writeBoolean(state.news.positive)
        }
        return bytes.toByteArray()
    }
    fun decode(bytes: ByteArray): GameState {
        require(bytes.size <= 65536) { "저장 파일이 너무 큽니다." }
        return DataInputStream(ByteArrayInputStream(bytes)).use { input ->
            require(input.readInt() == MAGIC && input.readInt() == 1) { "지원하지 않는 저장 형식입니다." }
            val day = input.readLong(); val seed = input.readLong()
            val cash = input.readLong(); val realized = input.readLong()
            val stocks = List(Market.companies.size) {
                val price = input.readLong(); val quantity = input.readLong(); val cost = input.readLong()
                val count = input.readInt()
                require(count in 1..30) { "가격 기록이 손상되었습니다." }
                Stock(price, List(count) { input.readLong() }, quantity, cost)
            }
            val reports = List(Market.companies.size) { input.readLong() }
            val due = input.readLong()
            val news = News(day, input.readInt(), input.readBoolean())
            require(input.available() == 0) { "저장 파일 끝에 알 수 없는 데이터가 있습니다." }
            Market.validate(GameState(day, seed, cash, realized, stocks, reports,
                if (due == 0L) null else Deposit(due), news))
        }
    }
}
