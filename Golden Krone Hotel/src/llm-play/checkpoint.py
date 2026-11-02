import sys
sys.path.insert(0,'llm-play')
from solve import observe,action
s=observe();action({'type':'command','command':'MENU'},s)
d=observe();action({'type':'choose','index':next(c['index'] for c in d['state']['menu']['choices'] if 'Exit Game' in c['text'])},d)
d=observe()
try:action({'type':'choose','index':next(c['index'] for c in d['state']['menu']['choices'] if 'Exit to desktop' in c['text'])},d)
except RuntimeError as e:
 if 'Bridge connection failed' not in str(e):raise
print('Saved')
