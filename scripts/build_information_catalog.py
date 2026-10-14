"""300 authored scenarios x 10 disclosure depths. Deterministic, no network/API required."""
import csv
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
COMPANIES = ['그린푸드', '픽셀게임즈', '노바테크', '솔라에너지', '브릿지물류', '온유메디컬', '한빛모터스', '두레건설', '해오름항공', '별빛엔터', '모아리테일', '누리텔레콤']
SECTORS = ['식품', '게임', '첨단 장비', '태양광', '운송', '의료 기기', '자동차', '건설', '항공', '콘텐츠', '유통', '통신']
CONTENT_PREFIXES = ('information', 'expansion-a', 'expansion-b')
EVENT_COUNT = 300
PRICES = [5000, 12000, 30000, 75000, 180000, 450000, 1200000, 3000000, 7500000, 20000000]
CONFIDENCE = [50, 56, 62, 68, 75, 82, 88, 93, 97, 99]
NAMES = ['희미한 소문', '목격 단서', '지역 취재', '업계 취재', '교차 조사', '기업 확인', '정밀 분석', '심층 검증', '전략 분석', '최상위 실사']
SOURCES = ['익명의 쪽지', '커뮤니티 갈무리', '지역신문', '업계 인터뷰', '현장 조사서', '공공문서 해설', '유통 동향지', '공급망 보고서', '계약 검토서', '종합 실사서']
STYLES = [3, 3, 0, 2, 1, 1, 0, 1, 1, 1]
VOICES = [
 ('돌아다니는 얘기', '같이 온 자료', '자료에 적힌 근거'),
 ('공유된 내용', '첨부 자료 발췌', '원문 출처'),
 ('', '업계 배경', '취재 자료'),
 ('Q. 어떤 소식인가', 'Q. 업계에서는 왜 주목하나', 'Q. 확인한 자료는'),
 ('조사 개요', '현장 여건', '대조 자료'),
 ('문서 요지', '적용 배경', '근거 문서'),
 ('유통 동향', '수요와 판매 여건', '확인 자료'),
 ('공급망 점검', '사업 구조', '관련 자료'),
 ('검토 의견', '거래 배경', '검토 문서'),
 ('요약', '사업 현황', '검증 자료'),
]
# Operational indicators and transmission mechanisms, specific to each category.
METRICS = {
 '수요': ('접수 주문량', '주문 증가가 매출로 연결되려면 납품이 끝나야 한다', False),
 '유통': ('판매 접점 수', '진열과 유통 접점의 변화가 판매 기회를 바꾼다', False),
 '무역': ('수출 가능 물량', '국경을 통과한 물량만 현지 판매로 연결된다', False),
 '원가': ('단위 조달 비용', '판매 단가가 같다면 원재료 비용이 이익을 좌우한다', True),
 '생산': ('가동 가능 물량', '생산량 변화는 고정비 부담과 납기 대응에 영향을 준다', False),
 '품질': ('검수 통과 물량', '검수에서 빠진 물량은 재작업과 대금 지연을 부른다', False),
 '계약': ('유효 계약 물량', '서명된 물량도 해지 조건과 검수를 통과해야 매출이 된다', False),
 '소비': ('구매 전환 지수', '관심이 늘어도 실제 결제로 이어지는지 확인해야 한다', False),
 '정책': ('사업 집행 가능 규모', '정책 발표와 실제 예산 집행 사이에는 시차가 있다', False),
 '물류': ('정시 도착 물량', '운송 지연은 재고와 납품 비용을 함께 바꾼다', False),
 '재무': ('필요 운전자금', '대금 회수와 지급 시점의 차이가 자금 부담을 결정한다', True),
 '기술': ('유효 처리량', '개선된 성능이 양산 환경에서도 재현되어야 한다', False),
 '인력': ('확보 작업 시간', '필요 인력을 제때 배치해야 일정과 생산량을 지킬 수 있다', False),
 '경쟁': ('확보 가능한 수요', '경쟁사의 빈자리가 실제 고객 이동으로 이어지는지 확인해야 한다', False),
 '기후': ('정상 운영 가능 시간', '날씨가 달라지면 가동과 물류 일정부터 영향을 받는다', False),
 '규제': ('허용 사업 범위', '규제 적용 시점과 예외 조항에 따라 사업 가능 범위가 달라진다', False),
 '행사': ('예상 접촉 고객 수', '행사에서 확보한 접점이 계약이나 결제로 이어져야 한다', False),
 '해외': ('해외 공급 가능 물량', '해외 진출은 승인 뒤 현지 유통과 정산까지 필요하다', False),
 '플랫폼': ('자연 유입 지수', '노출 변화가 이용자 유입과 결제 전환을 바꾼다', False),
 '운영': ('정상 서비스 시간', '운영 중단은 보상 비용과 고객 이탈을 함께 부른다', False),
 '브랜드': ('고객 관심 지수', '인지도 변화와 실제 매출 변화는 구분해서 봐야 한다', False),
 '보조금': ('지원 대상 사업 규모', '지원 대상에 포함되어도 지급 조건을 충족해야 한다', False),
 '광고': ('유효 방문 지수', '노출보다 실제 유입과 광고비 회수 여부가 중요하다', False),
 '결제': ('결제 처리 비용', '거래 규모가 같아도 수수료가 바뀌면 이익이 달라진다', True),
 '보안': ('정상 이용 지수', '보안 문제는 복구 비용뿐 아니라 고객 신뢰에도 영향을 준다', False),
 '금융': ('자금 조달 비용', '자금 조달 조건은 현금 여유와 투자 가능 규모를 바꾼다', True),
 '특허': ('보호 가능한 제품 범위', '권리 범위와 집행 가능성을 함께 확인해야 한다', False),
 '인증': ('출하 승인 물량', '인증 완료와 실제 주문 확보는 별도 단계다', False),
 '인허가': ('착수 가능한 공사 규모', '허가 뒤에도 부지와 자금 확보가 필요하다', False),
 '인프라': ('연결 가능한 처리량', '설비가 완성되어도 연결과 시운전을 끝내야 쓸 수 있다', False),
 '보험': ('자체 부담 예상 비용', '보장 범위와 자기 부담금이 최종 비용을 좌우한다', True),
 '저장': ('유효 저장 용량', '저장 가능량이 늘면 피크 수요에 대응할 여지가 커진다', False),
 '연구': ('완료 가능한 연구 단계', '연구 진척과 상업화 성공은 구분해야 한다', False),
 '예산': ('집행 가능 예산', '편성된 예산 중 실제 집행되는 금액을 확인해야 한다', False),
 '공급': ('확보 원부자재 물량', '조달 물량과 납기가 생산 일정을 제한한다', False),
}

def section(title, body):
    return f'{title} | {body}' if title else body

def compose(e, dossier, conversation, grade, i, positive, source_index):
    event = e['positive' if positive else 'negative']
    company = int(e['company'])
    metric, chain, reverse = METRICS[e['category']]
    expected = (1 if positive != reverse else -1) * (6 + (i * 7) % 23)
    spread = 3 + i % 5
    low, high = 100 + expected - spread, 100 + expected + spread
    lead, background, evidence = VOICES[source_index]
    informal = source_index in (0, 1)
    trace = conversation if informal else dossier['trace']
    mood = '일이 다시 돌아가는 분위기' if positive else '일정이 한 번 꼬인 분위기'
    context = dossier['background']
    # A forwarded message can quote a document without turning its narrator into a report writer.
    if informal:
        context = f'“{context}”\n같이 넘어온 자료에는 이렇게 적혀 있어.'
    if source_index == 3:
        context = 'A. ' + context
    if grade <= 2:
        title = ('전해 들은 얘기 · ' + e['scene'] if source_index == 0 else e['scene'] + ', 아는 사람?' if source_index == 1 else '현장 메모 · ' + e['scene'])
        if informal:
            paragraphs = [trace, f'전해 준 사람 말로는 {mood}래. 다만 그 사람이 현장 전체를 본 건 아니고, 왜 저렇게 해 놨는지도 못 물어봤대. 평소에 하던 작업일 수도 있지.',
                          '원본은 못 받았어. 장소랑 업체 이름도 빠져 있고 사진이 언제 찍힌 건지도 안 나와. 같은 얘기 들은 사람이 더 있는지는 모르겠어. 지금은 이 정도만 넘어온 상태야.']
        else:
            paragraphs = [section('현장 기록', trace), section('제보 내용', f'제보자는 현장에 대해 “{mood}”라고 전했다. 다만 전체 작업 일정과 변경 사유는 확인되지 않았다. 정기 작업을 별도 사안으로 받아들였을 가능성도 남아 있다.'),
                          section('확인 현황', '입수한 기록에는 장소와 업체명, 촬영 일시가 빠져 있다. 담당 부서의 설명이나 해당 일정을 뒷받침할 원문도 확보되지 않았다. 현재로서는 이 기록만으로 구체적인 사업 내용을 특정하기 어렵다.')]
        if grade == 2:
            paragraphs += ['덧붙이면, 내일쯤 움직임이 있을 거라는 말도 있대. 같은 글이 여러 군데 올라오긴 했는데 전부 한 사람한테서 나온 얘기일 수도 있어.' if informal else
                           section('일정 관련 제보', '예정 시점으로 내일이 거론되고 있다. 여러 경로로 유사한 내용이 전달됐으나, 같은 제보가 재전파된 것인지는 확인되지 않았다.')]
    elif grade == 3:
        title = f'{SECTORS[company]} 쪽에서 들은 얘기' if informal else f'{SECTORS[company]} 현장에 변화 조짐…세부 일정은 미확인'
        paragraphs = [section(lead, (f'{SECTORS[company]} 쪽에서 넘어온 얘기야. ' if informal else f'{SECTORS[company]} 업계에서 일정 변경 가능성이 거론되고 있다. ') + trace),
                      section(background, context),
                      f'보낸 사람은 {mood}라고 하더라. 다만 업체 이름이나 구체적인 일정은 안 적혀 있었어. 원문이 더 나와야 어느 쪽 일인지 알 수 있을 것 같아.' if informal else
                      section('추가 취재', f'제보자는 “{mood}”라고 설명했다. 해당 업체와 구체적인 일정은 확인되지 않았다. 일회성 작업인지 사업 계획의 변경인지는 추가 확인이 필요한 상황이다.')]
    else:
        title = (f'{e["topic"]}, 이런 얘기가 도네' if informal else event) if grade < 6 else f'{COMPANIES[company]} · {e["topic"]} 영향 점검'
        opening = [
            f'내일 {event[:-1]}다는 얘기가 돌아. {e["evidence"]} 쪽에서 나온 말이라는데, 내가 원본 전체를 본 건 아니야.',
            f'{e["topic"]} 관련해서 자료 하나 돌길래 옮겨 봄. 내용은 “{event}”는 거고, 일정은 내일로 적혀 있어.',
            f'입수한 {e["evidence"]}에 따르면 {event}. 관련 일정은 내일로 잡혀 있다.',
            f'A. 자료에는 “{event}”고 나와 있다. 예정 시점은 내일이다.',
            f'내일 예정된 {e["topic"]} 관련 현장을 점검했다. {e["evidence"]}상 주요 내용은 다음과 같다. {event}.',
            f'{e["evidence"]}에 담긴 주요 내용은 다음과 같다. {event}. 적용 예정일은 내일이다.',
            f'{event}. 내일 관련 일정이 진행되면 거래 조건과 물량 배분에도 변화가 예상된다.',
            f'{event}. 내일 예정된 변화가 관련 사업의 조달·운영 일정에 미칠 영향을 점검했다.',
            f'{e["evidence"]} 검토 결과, {event}. 아래 의견은 내일 예정된 일정과 현재 확보한 조건을 기준으로 한다.',
            f'{event}. {e["evidence"]} 및 현장 기록을 토대로 내일 예정된 변화의 사업 영향을 분석했다.',
        ][source_index]
        paragraphs = [section(lead, opening), section('현장 얘기' if informal else '현장 기록', trace), section(background, context)]
        if grade == 4:
            paragraphs += [f'다만 {e["risk"]} 얘기도 같이 붙어 있어. 계약 규모나 업체 이름은 아직 안 나왔고. 돌아다니는 건 일부 내용뿐이라, 발표대로 진행될지는 더 봐야 할 것 같아.' if informal else
                           section('남은 변수', f'{e["risk"]}도 변수로 꼽힌다. 관련 계약 규모와 참여 기업 명단은 아직 확인되지 않았다. 확보한 자료가 원문의 일부인 만큼 실제 이행 조건은 추가 취재가 필요하다.')]
        if grade >= 5:
            verification = '세부 조항과 담당자 확인은 아직 확보되지 않았다.' if grade < 7 else '적용 대상·예정 시점·담당자 설명을 대조했다. 이후 조건 변경 여부는 별도 확인이 필요하다.'
            paragraphs += [section(evidence, f'{e["evidence"]}. {verification}' if not informal else f'출처로 적힌 건 {e["evidence"]}야. ' + ('세부 조항이나 담당자 확인까지 붙어 있지는 않아.' if grade < 7 else '보낸 쪽에서는 적용 대상이랑 날짜를 담당자 설명과 맞춰 봤다고 해. 나중에 조건이 바뀌는지는 따로 봐야겠지.')),
                           section('자료에서 짚은 영향' if informal else '사업 영향', f'{chain}. ' + ('사업 여건 개선이 기대되지만 비용 부담에 따라 이익 기여는 달라질 수 있다.' if positive else '사업 여건에 부담이 예상되지만 기존 계약이나 대체 수단에 따라 영향은 달라질 수 있다.')),
                           section('함께 봐야 할 부분' if informal else '주요 변수', dossier['checkpoint'])]
        if grade >= 6:
            paragraphs += [section('첨부된 기업 분석' if informal else '기업 분석', f'분석 대상: {COMPANIES[company]}. {e["topic"]} 관련 사업의 {metric} 추정치는 기존 100 대비 {100+expected} 수준이다. 해당 변화가 실현된다는 가정으로 산출한 비교용 지수이며, 확정 실적이나 기업 전체 매출 전망을 뜻하지 않는다.')]
        if grade >= 7:
            paragraphs += [section('계약 조건', f'{e["risk"]} 관련 조항이 수익성의 변수다. 기존 계약에서 부담을 흡수할 수 있는지, 추가 비용이 기업에 남는지가 관건이다. 계약 변경이나 집행 지연이 발생하면 운영 지표의 변화와 실제 이익 사이에 차이가 생길 수 있다.')]
        if grade >= 8:
            paragraphs += [section('민감도 분석', f'{metric}: 기준 100, 예상 범위 {low}~{high}. {e["risk"]} 관련 조건에 따른 시나리오별 추정치다. 통계적 신뢰구간은 아니며, 지수 변화율이 매출 증가율이나 주가 등락률과 일치하지는 않는다.')]
        if grade >= 9:
            paragraphs += [section('반대 시나리오', ('추가 비용이 늘거나 기존 사업의 수요를 대체하는 데 그친다면 기대한 이익 개선이 나타나지 않을 수 있다.' if positive else '대체 경로를 확보하거나 불필요한 비용을 줄일 경우 예상보다 충격이 작을 수 있다.') + f' {e["evidence"]}의 적용 대상이나 집행 조건이 달라지면 본 전망도 수정이 필요하다.')]
        if grade == 10:
            impact = (100 + (i * 37) % 251) / 100
            paragraphs += [section('가격 영향 추정', f'해당 사안이 시세에 미치는 단독 영향은 {"+" if positive else "-"}{impact:.2f}%p로 산정했다. 운영 지수와 별도로 계산한 값이다. 기업의 기본 성장과 개장 시 단기 변동이 겹치므로 실제 등락률이나 방향과는 차이가 날 수 있다.'),
                           section('분석 범위', '본 자료는 내일 예정된 사안과 단기 사업 영향을 다룬다. 이후 실적과 장기 주가 전망은 포함하지 않는다. 공개 일정이나 계약 조건의 변경이 확인되면 추정치를 다시 산정할 필요가 있다.')]
    return title, '\n\n'.join(paragraphs)

def read_content(kind):
    records = []
    for prefix in CONTENT_PREFIXES:
        with (ROOT / f'content/{prefix}-{kind}.psv').open(encoding='utf-8-sig', newline='') as stream:
            batch = list(csv.DictReader(stream, delimiter='|'))
        assert len(batch) == 100, prefix
        assert all(None not in row and all(value and '\t' not in value and '\n' not in value for value in row.values()) for row in batch), prefix
        records.extend(batch)
    return records

def main():
    events = read_content('events')
    assert len(events) == EVENT_COUNT
    assert len({e['topic'] for e in events}) == EVENT_COUNT
    assert all(e['category'] in METRICS and 0 <= int(e['company']) < len(COMPANIES) for e in events)
    notes = read_content('dossiers')
    assert len(notes) == EVENT_COUNT and len({n['topic'] for n in notes}) == EVENT_COUNT
    dossiers = {n['topic']: n for n in notes}
    assert set(dossiers) == {e['topic'] for e in events}
    conversations = {n['topic']: n['trace'] for n in read_content('conversations')}
    assert len(conversations) == EVENT_COUNT and set(conversations) == set(dossiers)
    rows = []
    for grade in range(1, 11):
        for i, e in enumerate(events):
            company = int(e['company'])
            category, topic = e['category'], e['topic']
            impact = 100 + (i * 37) % 251
            price = PRICES[grade-1] * (80 + ((i*7) % 9) * 5) // 100
            source_index = (i + grade - 1) % len(SOURCES)
            bodies, titles = [], []
            for positive in (False, True):
                title, body = compose(e, dossiers[topic], conversations[topic], grade, i, positive, source_index)
                titles.append(title)
                bodies.append(body)
            source = SOURCES[source_index]
            row = dict(id=f'G{grade:02}-{i+1:03}', grade=grade, event=i, company=company, category=category,
                       source=source, style=STYLES[source_index], price=price, confidence=CONFIDENCE[grade-1], impact=impact,
                       negative_title=titles[0], positive_title=titles[1], negative_body=bodies[0], positive_body=bodies[1],
                       evidence=e['evidence'], risk=e['risk'], actual_negative=e['negative'], actual_positive=e['positive'])
            for positive in (False, True):
                key = 'report_positive_body' if positive else 'report_negative_body'
                row[key] = compose(e, dossiers[topic], conversations[topic], grade, i, positive, 9)[1] if grade in (6,8,10) else ''
            rows.append(row)
    assert Counter(r['grade'] for r in rows) == {g:EVENT_COUNT for g in range(1,11)}
    assert len({r['id'] for r in rows}) == EVENT_COUNT * 10
    for grade in range(1,11):
        subset = [r for r in rows if r['grade']==grade]
        assert len({r['positive_body'] for r in subset})==EVENT_COUNT
        assert len({r['negative_body'] for r in subset})==EVENT_COUNT
    dest=ROOT/'unity/Assets/Resources/information-catalog.txt'
    with dest.open('w',encoding='utf-8',newline='') as f:
        f.write('\t'.join(rows[0])+'\n')
        for row in rows:
            values=[str(v).replace('\n','\\n') for v in row.values()]
            assert all('\t' not in v and '\r' not in v for v in values)
            f.write('\t'.join(values)+'\n')
    with (ROOT/'docs/information-catalog.csv').open('w',encoding='utf-8-sig',newline='') as f:
        writer=csv.DictWriter(f,fieldnames=rows[0]);writer.writeheader();writer.writerows(rows)
    catalog_dir=ROOT/'docs/information-catalog';catalog_dir.mkdir(exist_ok=True)
    for grade in range(1,11):
        subset=[r for r in rows if r['grade']==grade]
        text=[f'# {grade}등급 · {NAMES[grade-1]} · {EVENT_COUNT}개', '',
              f"봉인 가격 {min(r['price'] for r in subset):,}~{max(r['price'] for r in subset):,}원 · 신뢰도 {CONFIDENCE[grade-1]}%", '',
              '집필용 전체 목록입니다. 기업과 사건은 구매 전 봉인 화면에 표시되지 않습니다. 같은 사건의 긍정/부정 판본 중 세계 상태에 맞춰 하나를 판매합니다.', '']
        for r in subset:
            text += [f"## {r['id']} · {events[r['event']]['topic']}", '',
                     f"{COMPANIES[r['company']]} / {r['category']} / {r['source']} / {r['price']:,}원", '',
                     '**긍정 판본**', '', r['positive_title'], '', r['positive_body'], '',
                     '**부정 판본**', '', r['negative_title'], '', r['negative_body'], '']
            if grade in (6,8,10):
                text += ['**기업 지정 분석 · 긍정 판본**', '', r['report_positive_body'], '',
                         '**기업 지정 분석 · 부정 판본**', '', r['report_negative_body'], '']
        (catalog_dir/f'grade-{grade:02}.md').write_text('\n'.join(text),encoding='utf-8')
    sample = ['# 매체별 문체 비교', '', '게임박람회 4등급 긍정 판본을 같은 조건에서 비교한 편집용 예시입니다. 실제 판매 매체는 당일 상품에 따라 정해집니다.', '']
    event = events[17]
    for source_index, source in enumerate(SOURCES):
        title, body = compose(event, dossiers[event['topic']], conversations[event['topic']], 4, 17, True, source_index)
        sample += ['## ' + source, '', title, '', body, '']
    (catalog_dir/'style-samples.md').write_text('\n'.join(sample), encoding='utf-8')
    print(f'Wrote {len(rows)} products; {EVENT_COUNT} per grade; {len(COMPANIES)} companies; {len(METRICS)} categories; 10 media. Stable authored events: {len(events)}.')

if __name__ == '__main__': main()
