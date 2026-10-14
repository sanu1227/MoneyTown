package com.moneytown.game

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.SystemBarStyle
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.semantics.LiveRegionMode
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.liveRegion
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import java.util.Locale

private val Mint = Color(0xFF5BDDCA)
private val Muted = Color(0xFFA5B7CA)
private val Panel = Color(0xFF152838)
private val Background = Color(0xFF0B1420)
private fun money(value: Long) = "%,d원".format(Locale.KOREA, value)

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge(
            statusBarStyle = SystemBarStyle.dark(android.graphics.Color.TRANSPARENT),
            navigationBarStyle = SystemBarStyle.dark(android.graphics.Color.TRANSPARENT)
        )
        val store = GameStore(applicationContext)
        setContent {
            MaterialTheme(colorScheme = darkColorScheme(
                primary = Mint, onPrimary = Background, background = Background,
                surface = Panel, onSurface = Color(0xFFE5EDF7),
                secondaryContainer = Color(0xFF17413F), onSecondaryContainer = Mint
            )) { MoneyTown(store) }
        }
    }
}

@Composable
private fun MoneyTown(store: GameStore) {
    val loaded = remember { runCatching { store.load() ?: Market.create() } }
    var state by remember { mutableStateOf(loaded.getOrElse { Market.create() }) }
    var blocked by remember { mutableStateOf(loaded.isFailure) }
    var selected by rememberSaveable { mutableIntStateOf(0) }
    var tab by rememberSaveable { mutableIntStateOf(0) }
    var quantity by rememberSaveable { mutableStateOf("1") }
    var message by rememberSaveable { mutableStateOf(
        if (loaded.isFailure) "저장 파일을 읽지 못했습니다. 기존 파일은 보존됩니다. 자산 탭에서 초기화할 수 있습니다."
        else "뉴스를 확인하고 첫 투자를 시작하세요."
    ) }
    var confirmReset by remember { mutableStateOf(false) }
    fun act(success: String, action: () -> GameState) {
        if (blocked) return
        val next = try { action() } catch (error: Exception) {
            message = if (error is ArithmeticException) "거래 금액이 한도를 초과했습니다." else error.message ?: "입력값을 확인하세요."
            return
        }
        try {
            // 현재 저장은 2KB 이하입니다. 성공한 저장만 화면 상태에 반영합니다.
            store.save(next)
            state = next
            message = success
        } catch (_: Exception) {
            message = "저장하지 못해 이번 행동을 취소했습니다. 기기 저장 공간을 확인하세요."
        }
    }
    Scaffold(
        containerColor = Background,
        bottomBar = {
            NavigationBar(containerColor = Panel) {
                listOf("주식", "뉴스·정보", "자산").forEachIndexed { index, title ->
                    NavigationBarItem(selected = tab == index, onClick = { tab = index },
                        icon = { Text(listOf("↗", "▤", "₩")[index], fontSize = 22.sp) },
                        label = { Text(title) })
                }
            }
        }
    ) { padding ->
        Column(Modifier.fillMaxSize().padding(padding).imePadding()
            .verticalScroll(rememberScrollState()).padding(16.dp), verticalArrangement = Arrangement.spacedBy(14.dp)) {
            Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.SpaceBetween) {
                Column { Text("MoneyTown", fontSize = 28.sp, fontWeight = FontWeight.Bold); Text("나만의 투자 일지", color = Mint, fontSize = 12.sp) }
                Text("${state.day}일차", fontWeight = FontWeight.Bold)
            }
            Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                Summary("보유 현금", money(state.cash), Modifier.weight(1f))
                Summary("총자산", money(Market.total(state)), Modifier.weight(1f))
            }
            Text(message, color = if (blocked) Color(0xFFFFB0B7) else Muted, fontSize = 13.sp,
                modifier = Modifier.semantics { liveRegion = LiveRegionMode.Polite })
            Button(onClick = { act("하루가 지났습니다. 가격과 새 뉴스를 확인하세요.") { Market.nextDay(state) } },
                enabled = !blocked, modifier = Modifier.fillMaxWidth()) { Text("다음 날 진행") }
            when (tab) {
                0 -> {
                    Section("주식 거래") {
                        Market.companies.indices.chunked(3).forEach { row ->
                            Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                                row.forEach { i ->
                                    FilterChip(selected = selected == i, onClick = { selected = i },
                                        modifier = Modifier.weight(1f).heightIn(min = 48.dp),
                                        label = { Text(Market.companies[i].name, fontSize = 11.sp) })
                                }
                            }
                        }
                        val company = Market.companies[selected]
                        val stock = state.stocks[selected]
                        Text(company.name, fontSize = 22.sp, fontWeight = FontWeight.Bold)
                        Text("${company.sector} · 가상 기업", color = Muted, fontSize = 12.sp)
                        Text(money(stock.price), fontSize = 30.sp, fontWeight = FontWeight.Bold)
                        val previous = stock.history.getOrElse(stock.history.lastIndex - 1) { stock.price }
                        val change = (stock.price.toDouble() / previous - 1) * 100
                        Text("전일 대비 %+.2f%%".format(Locale.KOREA, change), color = if (change < 0) Color(0xFF9BC8FF) else Color(0xFFFFB0B7))
                        PriceChart(stock)
                        Text("최저 ${money(stock.history.min())} · 최고 ${money(stock.history.max())}", fontSize = 12.sp, color = Muted)
                        Text("보유 ${stock.quantity}주 · 평균 원가 ${money(if (stock.quantity == 0L) 0 else stock.cost / stock.quantity)}")
                        Text("평가손익 ${money(stock.price * stock.quantity - stock.cost)}", color = Mint)
                        Text("평가손익은 매도 수수료 제외", color = Muted, fontSize = 12.sp)
                        OutlinedTextField(value = quantity, onValueChange = { if (it.length <= 16) quantity = it },
                            label = { Text("거래 수량") }, singleLine = true,
                            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                            modifier = Modifier.fillMaxWidth())
                        val order = runCatching { Market.quote(state, selected, quantity.toLongOrNull() ?: 0) }.getOrNull()
                        Text(if (order == null) "1 이상의 정수 수량을 입력하세요."
                            else "매수 ${money(order.buy)} / 매도 ${money(order.sell)}\n수수료 각각 ${money(order.fee)}", color = Muted, fontSize = 12.sp)
                        Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                            Button(onClick = { act("${company.name} 매도 완료.") { Market.trade(state, selected, quantity.toLongOrNull() ?: 0, false) } },
                                enabled = !blocked && order != null, modifier = Modifier.weight(1f),
                                colors = ButtonDefaults.buttonColors(containerColor = Color(0xFF94434D), contentColor = Color.White)) { Text("매도") }
                            Button(onClick = { act("${company.name} 매수 완료.") { Market.trade(state, selected, quantity.toLongOrNull() ?: 0, true) } },
                                enabled = !blocked && order != null, modifier = Modifier.weight(1f)) { Text("매수") }
                        }
                    }
                    Section("오늘의 뉴스") {
                        Text(state.news.title)
                        TextButton(onClick = { tab = 1 }) { Text("뉴스와 기업 정보 보기") }
                    }
                }
                1 -> {
                    Section("오늘의 시장 뉴스") {
                        Text("${state.news.day}일차 · 가상 경제신문", color = Mint, fontSize = 12.sp)
                        Text(state.news.title, fontSize = 20.sp, fontWeight = FontWeight.Bold)
                        Text("공개 기업 뉴스입니다. 다음 날 가격에 영향을 주며 다른 변동도 함께 반영됩니다.", color = Muted)
                    }
                    Section("${Market.companies[selected].name} 분석 보고서") {
                        val company = Market.companies[selected]
                        val valid = state.reports[selected] >= state.day
                        Text("주식 탭에서 분석할 기업을 선택할 수 있습니다.", color = Muted, fontSize = 12.sp)
                        if (valid) {
                            Text("장기 성장 성향: ${if (company.growth == .002) "높음" else "보통"}")
                            Text("가격 변동성: ${if (company.volatility >= .035) "높음" else "보통"}")
                            Text("${state.reports[selected]}일차까지 유효 · 뉴스와 반대 방향으로 움직일 수 있습니다.")
                        } else Text(if (state.reports[selected] > 0) "이전 보고서가 만료되었습니다." else "성장 성향과 변동성을 분석한 보고서입니다.")
                        Button(onClick = { act("기업 분석 보고서를 구매했습니다.") { Market.buyReport(state, selected) } }, enabled = !blocked && !valid) { Text("보고서 구매 · 2,000원") }
                        Text("구매일부터 3개 거래일 유효 · 수익 보장 없음", color = Muted, fontSize = 12.sp)
                    }
                }
                2 -> {
                    Section("내 투자 자산") {
                        Text("전체 실현손익 ${money(state.realized)}", fontWeight = FontWeight.Bold)
                        val held = state.stocks.withIndex().filter { it.value.quantity > 0 }
                        if (held.isEmpty()) Text("아직 보유한 주식이 없습니다.", color = Muted)
                        held.forEach { (i, stock) ->
                            Text("${Market.companies[i].name} ${stock.quantity}주 · ${money(stock.price * stock.quantity)}")
                        }
                    }
                    Section("3일 정기 예금") {
                        val deposit = state.deposit
                        Text(if (deposit == null) "100,000원을 맡기면 3일 뒤 100,500원을 받습니다."
                            else "100,000원 예치 중\n${deposit.due}일차에 100,500원 자동 반환")
                        Button(onClick = { act("예금 가입 완료. 3일 뒤 자동 반환됩니다.") { Market.deposit(state) } },
                            enabled = !blocked && deposit == null) { Text("예금 가입 · 100,000원") }
                        Text("중도 해지 불가 · 게임용 수익률", color = Muted, fontSize = 12.sp)
                    }
                    Section("게임 관리") {
                        Text("진행은 이 기기에 자동 저장됩니다. 앱을 닫은 동안에는 날짜가 진행되지 않습니다.")
                        OutlinedButton(onClick = { confirmReset = true }) { Text("새 게임 시작") }
                        Text("모든 기업·뉴스·금액은 게임 속 가상 정보입니다.", color = Muted, fontSize = 12.sp)
                    }
                }
            }
            Spacer(Modifier.height(8.dp))
        }
    }
    if (confirmReset) AlertDialog(onDismissRequest = { confirmReset = false },
        title = { Text("새 게임을 시작할까요?") },
        text = { Text("현재 진행을 초기화하고 1,000,000원으로 다시 시작합니다. 기존 저장 파일은 앱 내부에 백업됩니다.") },
        confirmButton = { TextButton(onClick = {
            try {
                state = store.reset(); blocked = false; selected = 0; quantity = "1"; tab = 0
                message = "새 게임을 시작했습니다."
            } catch (_: Exception) { message = "초기화하지 못했습니다. 저장 공간을 확인하세요." }
            confirmReset = false
        }) { Text("새 게임 시작") } },
        dismissButton = { TextButton(onClick = { confirmReset = false }) { Text("취소") } })
}

@Composable
private fun Summary(label: String, value: String, modifier: Modifier) {
    Column(modifier.background(Panel, RoundedCornerShape(16.dp)).padding(14.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
        Text(label, color = Muted, fontSize = 12.sp)
        Text(value, fontSize = 18.sp, fontWeight = FontWeight.Bold)
    }
}

@Composable
private fun Section(title: String, content: @Composable ColumnScope.() -> Unit) {
    Column(Modifier.fillMaxWidth().background(Panel, RoundedCornerShape(18.dp)).padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(10.dp)) {
        Text(title, fontWeight = FontWeight.Bold, fontSize = 17.sp)
        content()
    }
}

@Composable
private fun PriceChart(stock: Stock) {
    Canvas(Modifier.fillMaxWidth().height(150.dp).background(Background, RoundedCornerShape(10.dp))
        .semantics { contentDescription = "최근 ${stock.history.size}개 거래일 주가. 현재 ${money(stock.price)}, 최저 ${money(stock.history.min())}, 최고 ${money(stock.history.max())}" }) {
        val margin = 14.dp.toPx()
        val low = stock.history.min().toDouble()
        val span = (stock.history.max() - low).coerceAtLeast(1.0)
        val points = stock.history.mapIndexed { i, price ->
            Offset(margin + i * (size.width - margin * 2) / (stock.history.size - 1).coerceAtLeast(1),
                size.height - margin - ((price - low) / span * (size.height - margin * 2)).toFloat())
        }
        for (part in 1..3) drawLine(Color(0xFF294154), Offset(margin, size.height * part / 4), Offset(size.width - margin, size.height * part / 4))
        val path = Path().apply {
            moveTo(points.first().x, points.first().y)
            points.drop(1).forEach { lineTo(it.x, it.y) }
        }
        drawPath(path, Mint, style = Stroke(width = 2.dp.toPx()))
        drawCircle(Mint, radius = 4.dp.toPx(), center = points.last())
    }
}
