"""Editorial coverage and compatibility checks for every published variant."""
import csv
import hashlib
import re
from pathlib import Path
from collections import Counter
from build_information_catalog import COMPANIES, SOURCES, STYLES, METRICS, EVENT_COUNT, read_content

ROOT = Path(__file__).resolve().parents[1]

def read(relative, delimiter):
    with (ROOT / relative).open(encoding='utf-8-sig', newline='') as stream:
        return list(csv.DictReader(stream, delimiter=delimiter))

notes = read_content('dossiers')
conversations = {row['topic']: row['trace'] for row in read_content('conversations')}
events = read_content('events')
rows = read('docs/information-catalog.csv', ',')
archives = []
for edition, expected_hash in ((1, '0e0c4b704381ee570c47a909496e9b56b1c2b6ba2454c169da1daa24ba99151b'),
                               (2, 'b36aee8fabcaac3abb3df03e2d435431d47e8c13faa0b43775f59a1ba26c38ec')):
    relative = f'unity/Assets/Resources/information-catalog-v{edition}.txt'
    assert hashlib.sha256((ROOT / relative).read_bytes()).hexdigest() == expected_hash
    archive = read(relative, '\t')
    assert len(archive) == 1000
    archives.append({row['id']: row for row in archive})
runtime = read('unity/Assets/Resources/information-catalog.txt', '\t')
assert len(rows) == len(runtime) == EVENT_COUNT * 10 == 3000
assert len(notes) == len(events) == EVENT_COUNT
assert len({event['topic'] for event in events}) == EVENT_COUNT
assert set(int(event['company']) for event in events) == set(range(12))
assert all(event['category'] in METRICS for event in events)
assert len(conversations) == EVENT_COUNT and set(conversations) == {event['topic'] for event in events}
assert len(set(conversations.values())) == EVENT_COUNT
assert Counter(int(row['grade']) for row in rows) == {grade: EVENT_COUNT for grade in range(1, 11)}
assert len({row['id'] for row in rows}) == len(rows)
dossiers = {note['topic']: note for note in notes}
for field in ('trace', 'background', 'checkpoint'):
    assert len({note[field] for note in notes}) == EVENT_COUNT, field
stable = ('id', 'grade', 'event', 'company', 'category', 'source', 'price', 'confidence', 'impact', 'actual_negative', 'actual_positive')
for row, asset in zip(rows, runtime):
    for archive in archives:
        old = archive.get(row['id'])
        if old is not None:
            assert all(row[field] == old[field] for field in stable), row['id']
    # The existing second-edition articles are unchanged; archived purchases keep exact wording.
    if row['id'] in archives[1]:
        assert all(value == archives[1][row['id']][key].replace('\\n', '\n') for key, value in row.items()), row['id']
    assert all(value == asset[key].replace('\\n', '\n') for key, value in row.items()), row['id']
    grade = int(row['grade'])
    note = dossiers[events[int(row['event'])]['topic']]
    informal = row['source'] in SOURCES[:2]
    assert int(row['style']) == STYLES[SOURCES.index(row['source'])]
    for direction in ('negative', 'positive'):
        body = row[direction + '_body']
        assert len(body) >= 200 and body.count('\n\n') >= 2, row['id']
        assert (conversations[note['topic']] if informal else note['trace']) in body, row['id']
        if grade >= 3:
            assert note['background'] in body, row['id']
        if grade >= 5:
            assert note['checkpoint'] in body, row['id']
        if grade < 6:
            assert not any(company in body for company in COMPANIES), row['id']
        else:
            assert COMPANIES[int(row['company'])] in body and '비교용 지수' in body, row['id']
        if grade >= 9:
            assert '반대 시나리오 |' in body, row['id']
        # '판매도 한다' is ordinary retail prose, not an instruction to sell a stock.
        assert re.search(r'(?<![가-힣])매[수도]', body) is None, row['id']
        old = archives[0].get(row['id'])
        if old is not None:
            assert body != old[direction + '_body'].replace('\\n', '\n'), row['id']
        for phrase in ('취재본', '판단의 경계', '게임 모델', '남겨진 빈칸', '조건까지 읽는 분석', '아직 지나지 않은 사건'):
            assert phrase not in body + row[direction + '_title'], (row['id'], phrase)
        report = row['report_' + direction + '_body']
        if grade in (6, 8, 10):
            assert len(report) >= 500 and COMPANIES[int(row['company'])] in report, row['id']
            assert note['trace'] in report and note['checkpoint'] in report and '비교용 지수' in report, row['id']
            assert all(phrase not in report for phrase in ('얘기야', '더라.', '옮겨 봄', '게임 모델')), row['id']
        else:
            assert report == '', row['id']
for grade in range(1, 11):
    lengths = [len(row['positive_body']) for row in rows if int(row['grade']) == grade]
    subset = [row for row in rows if int(row['grade']) == grade]
    assert all(len({row[direction + '_body'] for row in subset}) == EVENT_COUNT for direction in ('positive', 'negative'))
    print(f'Grade {grade}: {EVENT_COUNT} articles, {min(lengths)}-{max(lengths)} characters')
print('PASS: 6000 media variants, 1800 company reports, 300 conversational traces, 12 companies, unchanged legacy economics and two archives.')
