"""Summarises a Unity NUnit result XML: totals and every failed test with the start of its message.
Usage: python Tools/test_summary.py Logs/<tag>-EditMode.xml [name filter]"""
import re
import sys

path = sys.argv[1]
only = sys.argv[2] if len(sys.argv) > 2 else None
text = open(path, encoding='utf-8').read()
print(re.search(r'<test-run[^>]*>', text).group(0)[:170])
for m in re.finditer(r'<test-case [^>]*?fullname="([^"]+)"[^>]*?result="Failed"[^>]*>(.*?)</test-case>', text, re.S):
    name = m.group(1)
    if only and only not in name:
        continue
    msg = re.search(r'<message><!\[CDATA\[(.*?)\]\]>', m.group(2), re.S)
    print('FAIL', '.'.join(name.split('.')[-2:]), (msg.group(1)[:170].replace('\n', ' ') if msg else ''))
if only:
    passed = len(re.findall(r'fullname="[^"]*' + re.escape(only) + r'[^"]*"[^>]*result="Passed"', text))
    print(f'{only}: {passed} passed')
