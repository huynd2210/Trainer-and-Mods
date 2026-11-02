"""Resume the locked saved run through normal title inputs, without focusing NW."""
import time
import uuid
from gkh import Bridge

b=Bridge()
s=b.call('/state')
if not s['exclusive']:
    raise RuntimeError('Human owns input; do not automatically reclaim control')
b.call('/control',{'enabled':True})
for i in range(200):
    try:s=b.call('/state')
    except RuntimeError as e:
        if 'connection failed' not in str(e).lower():raise
        time.sleep(.5);continue
    if s['state']=='running':
        print('RUNNING',s['turn'],s['floor'],s['player'],flush=True);break
    if not s['enabled'] or not s['exclusive']:raise RuntimeError('Human handoff; no next input')
    if s['busy'] or s['state'] in ('loading','vulgat'):time.sleep(.5);continue
    if s['state'] not in ('updateSplash','title'):raise RuntimeError('Unexpected phase: '+s['state'])
    a={'type':'command','command':'CONFIRM','expectedRevision':s['revision'],'requestId':uuid.uuid4().hex}
    r=b.call('/action',a)
    print(r['state']['state'],flush=True)
    time.sleep(.2)
else:raise RuntimeError('Saved run has not finished loading')
