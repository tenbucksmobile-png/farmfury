"""
Farm Fury - block support check for trimmed colliders (2026-11-08).

BlockBase trims a block's collider to its visible art on levels with LevelData.referenceFraming (the sprites
carry transparent padding). A level laid out against the padded rects can then have blocks resting over a gap,
which BlockBase.SettleIfUnsupported wakes at level start (they drop or the structure collapses). This lists, for
every level asset (World 2 skipped: ice art), the blocks that have support with full-rect colliders but lose it
when trimmed. Run it before turning on referenceFraming for a level; an empty report means it is safe.

Mirrors BlockBase's sprite choice (WoodArtVariant + the Auto aspect rule) and SettleIfUnsupported's 0.08 u probe.
Usage (repo root):  python tools/check_block_support.py [level-name-filter]
"""
import sys
import re, glob, os, numpy as np
os.chdir(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'unity', 'Assets'))
from PIL import Image
P='Sprites/Environment/World1Props/'
bb={}
def content(name):
    if name not in bb:
        im=Image.open(P+name+'.png').convert('RGBA'); a=np.asarray(im)[...,3]>0; ys,xs=np.nonzero(a); w,h=im.size
        # in sprite local fractions, y up
        bb[name]=(xs.min()/w, (xs.max()+1)/w, 1-(ys.max()+1)/h, 1-ys.min()/h)
    return bb[name]
V=['Auto','Flat','Horizontal','Vertical','Short','VerticalShort','Horizontal2D','Shork2D','Shork','Cart','Barrel','Square','Block','RuinedWall','Tower','Skew','Diagonal']
wood=dict(normal='Plank_Horizontal',Horizontal='Plank_Horizontal',Vertical='2D_Block_Wood_Vertical',Short='Plank_Short',Skew='Plank_Skew',Diagonal='Plank_Diagonal',VerticalShort='Plank_VeriticalShort',Horizontal2D='Plank_2DHorizontal',Shork2D='Plank_2DShork',Shork='Plank_Shork',Cart='WoodenCart',Barrel='WoodenBarrel',Flat='2D_Block_Wood_Flat')
stone=dict(normal='Stone_Block',Horizontal='Stone_Block',Vertical='Stone_Vertical',Skew='Stone_Skew',Diagonal='Stone_Diagonal',Square='Stone_Square',Block='Stone_Block',RuinedWall='RuinedStoneWall',Tower='StoneTower')
def sprite(t,var,w,h):
    if t==2: return 'Haybail'
    if t==3: return 'Barrel_Dynamite'
    m=wood if t==0 else stone
    if var=='Auto':
        a=w/h; var='Vertical' if a<0.72 else 'Horizontal' if a>1.5 else 'normal'
    fb={'VerticalShort':['VerticalShort','Vertical'],'Horizontal2D':['Horizontal2D','Horizontal'],'Shork2D':['Shork2D','Vertical']}.get(var,[var])
    for f in fb:
        if f in m: return m[f]
    return m['normal']
def boxes(blocks,trim):
    out=[]
    for b in blocks:
        x,y,w,h=b['x'],b['y'],b['w'],b['h']
        if trim:
            s=sprite(b['t'],b['v'],w,h)
            if not os.path.exists(P+s+'.png'): print('missing',s); x0,x1,y0,y1=0,1,0,1
            else: x0,x1,y0,y1=content(s)
            if b['flip']: x0,x1=1-x1,1-x0
            out.append((x-w/2+x0*w, x-w/2+x1*w, y-h/2+y0*h, y-h/2+y1*h))
        else: out.append((x-w/2,x+w/2,y-h/2,y+h/2))
    return out
def unsupported(blocks,ground,trim):
    B=boxes(blocks,trim); res=[]
    for i,(l,r,bt,tp) in enumerate(B):
        cx=(l+r)/2; hw=(r-l)*0.45; y0,y1=bt-0.08,bt
        ok = ground>=y0
        for j,(l2,r2,b2,t2) in enumerate(B):
            if j==i: continue
            if l2<cx+hw and r2>cx-hw and b2<y1 and t2>y0: ok=True;break
        if not ok and not blocks[i]['stay']: res.append(i)
    return res
tot=0
flt = sys.argv[1] if len(sys.argv) > 1 else ''
for f in sorted(glob.glob('ScriptableObjects/Levels/*.asset')):
    if flt and flt not in os.path.basename(f): continue
    s=open(f,encoding='utf-8').read()
    world=int(re.search(r'\n  world: (\d+)',s).group(1))
    if world==2: continue
    framed=re.search(r'referenceFraming: 1',s) is not None
    blk=s.split('  blocks:')[1].split('  robots:')[0]
    items=re.split(r'\n  - type: ',blk)[1:]
    blocks=[]
    for it in items:
        t=int(it.split('\n')[0]); pos=re.search(r'position: \{x: ([-\d.e]+), y: ([-\d.e]+)\}',it); sz=re.search(r'size: \{x: ([-\d.e]+), y: ([-\d.e]+)\}',it)
        v=int(re.search(r'artVariant: (\d+)',it).group(1)); fl=re.search(r'flipX: (\d)',it); st=re.search(r'forceStayKinematic: (\d)',it)
        blocks.append(dict(t=t,x=float(pos.group(1)),y=float(pos.group(2)),w=float(sz.group(1)),h=float(sz.group(2)),v=V[v],flip=fl and fl.group(1)=='1',stay=bool(st and st.group(1)=='1')))
    g=-5.6 if framed else -6.6
    a=set(unsupported(blocks,g,False)); b=set(unsupported(blocks,g,True))
    new=b-a
    if framed: print(os.path.basename(f), '(framed) unsupported when trimmed:', sorted(b))
    if new:
        tot+=1
        print(os.path.basename(f), 'newly unsupported:', [(i,['W','S','H','B'][blocks[i]['t']],blocks[i]['v'],round(blocks[i]['x'],2),round(blocks[i]['y'],2)) for i in sorted(new)], 'already:',len(a))
print('levels affected',tot)
