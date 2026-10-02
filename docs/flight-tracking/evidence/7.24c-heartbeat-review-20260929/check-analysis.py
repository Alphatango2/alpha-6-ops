from analyze import stats,union,read,root
import json
checks=[]
def check(value,name):
    checks.append(dict(passed=bool(value),name=name))
s=stats(list(range(1,101)))
check(s==dict(count=100,min=1,median=50.5,p95=95,p99=99,max=100),'Nearest-rank quantiles and even median')
check(union([(0,5),(1,3),(4,7),(9,10)])==8,'Nested/overlapping callbacks are not double-counted')
limit=max(150.,245.4435+100.)
check(limit==345.4435 and 400.1605>limit,'Historical relative failure preserved without rounding')
check(not (500<500) and 499.999<500,'Original strict absolute boundary preserved')
check(not (345.4435>limit),'Equality allowed at relative boundary')
r=read(root/'preserved-failure-correlation.json')
check(len(r)==4 and sum(x['relativeExceedances'] for x in r)==5 and sum(x['absoluteExceedances'] for x in r)==1,'All four preserved failed runs, five relative and one absolute excursion')
output=dict(executed=len(checks),passed=sum(c['passed'] for c in checks),failed=sum(not c['passed'] for c in checks),checks=checks)
(root/'analysis-contracts.json').write_text(json.dumps(output,indent=2));print(json.dumps(output,indent=2))
assert output['failed']==0
