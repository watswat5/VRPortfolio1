"""Build a static low-poly highway prefab and connect it to SampleScene's Highway root.
Uses native Unity cube meshes and the existing asphalt and steel materials.
Run from the project root. The existing motel and parking surface are preserved.
"""
from pathlib import Path
import math
import re
import uuid
import json

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Assets/Environment/Highway'
SCENE = ROOT / 'Assets/Scenes/SampleScene.unity'
OUT.mkdir(parents=True, exist_ok=True)

def guid(name):
    return uuid.uuid5(uuid.NAMESPACE_URL, 'scavenger/highway/' + name).hex

def meta(path, importer='NativeFormatImporter', file_id=2100000):
    path.with_name(path.name + '.meta').write_text(
        f'fileFormatVersion: 2\nguid: {guid(path.name)}\n{importer}:\n  externalObjects: {{}}\n' +
        (f'  mainObjectFileID: {file_id}\n' if importer == 'NativeFormatImporter' else '') +
        '  userData: \n  assetBundleName: \n  assetBundleVariant: \n')

def material(name, color):
    template = (ROOT / 'Assets/Models/VendingMachine/Generated/Cream.mat').read_text()
    template = template.split('--- !u!114')[0]
    template = template.replace('m_Name: Cream', 'm_Name: ' + name)
    rgba = '{r: %s, g: %s, b: %s, a: 1}' % color
    for prop in ('_BaseColor', '_Color'):
        template = re.sub(r'(' + prop + r': )\{[^}]+\}', lambda m: m[1] + rgba, template)
    template = template.replace('m_EnableInstancingVariants: 0', 'm_EnableInstancingVariants: 1')
    path = OUT / (name + '.mat')
    path.write_text(template)
    meta(path)
    return (2100000, guid(path.name), 2)

materials = {
    'asphalt': (-876546973899608171, '02837f5784c41bc46a01d3b5fefd7de5', 3),
    'steel': (2100000, '19d17e2b03806d544969d8abe0adb4ab', 2),
    'white': material('Faded Ivory Paint', (.78, .75, .64)),
    'yellow': material('Ochre Road Paint', (.84, .57, .12)),
    'shoulder': material('Dusty Shoulder', (.38, .30, .22)),
    'concrete': material('Wheel Stop Concrete', (.46, .44, .37)),
}

nodes = []
def node(name, parent=None, position=(0,0,0), scale=(1,1,1), mat=None, collision=False, floor=False, rotation=(0,0,0,1)):
    n = dict(id=100000 + len(nodes)*10, name=name, parent=parent, position=position,
             scale=scale, mat=mat, collision=collision, floor=floor, rotation=rotation)
    nodes.append(n)
    return n

root = node('Highway and Parking')
road = node('Road and Entrance', root)
paint = node('Highway Markings', root)
parking = node('Parking - 8 bays', root)
barriers = node('Metal Guardrails', root)

def box(name, parent, position, size, mat, collision=False, floor=False, rotation=(0,0,0,1)):
    return node(name,parent,position,size,mat,collision,floor,rotation)

# Two 3.6m lanes with shoulders. The road runs east-west in front of the motel.
box('Compacted shoulder', road, (4.5,-.055,-19), (180,.1,12.4), 'shoulder')
box('Two lane highway', road, (4.5,-.05,-19), (180,.12,9.6), 'asphalt', True, True)
# Flush with the existing lot at Y=.01; overlaps its south edge to avoid a seam.
box('Motel access driveway', road, (11.5,-.05,-11.8), (8,.12,6), 'asphalt', True, True)
for z in (-19.12,-18.88):
    box('Double yellow centerline',paint,(4.5,.019,z),(180,.012,.105),'yellow')
# Roadside lines break across the entrance; the centerline stays continuous.
box('Far edge line',paint,(4.5,.019,-22.65),(180,.012,.12),'white')
for a,b in [(-85.5,7.5),(15.5,94.5)]:
    box('Near edge line',paint,((a+b)/2,.019,-15.35),(b-a,.012,.12),'white')
box('Driveway stop line',parking,(9.45,.019,-14.7),(3.6,.012,.24),'white')

# Eight 2.8m x 5m spaces, leaving a 5.5m maneuvering aisle to the south.
for i in range(9):
    x=-7.1+i*2.8
    box(f'Bay divider {i+1:02}',parking,(x,.022,-2.4),(.10,.014,5),'white')
box('Parking head line',parking,(4.1,.022,.10),(22.5,.014,.10),'white')
for i in range(8):
    x=-5.7+i*2.8
    box(f'Wheel stop {i+1:02}',parking,(x,.10,-.35),(1.65,.18,.20),'concrete',True)

# Corrugated-looking twin ribs, regularly spaced steel posts and rounded-end silhouettes.
def guardrail(name, a, b, z):
    group=node(name,barriers)
    length=b-a
    box('Rail web',group,((a+b)/2,.67,z),(length,.32,.055),'steel',True)
    for height in (.57,.77):
        box('Pressed steel rib',group,((a+b)/2,height,z+.035),(length,.085,.11),'steel')
    count=math.ceil(length/3)
    for i in range(count+1):
        x=a+length*i/count
        box('Galvanized post',group,(x,.44,z-.09),(.12,.88,.12),'steel')
        if i%3==0:
            box('Amber reflector',group,(x,.72,z+.1),(.09,.13,.012),'yellow')
    for x in (a,b):
        box('Blunt end cap',group,(x,.67,z+.025),(.18,.36,.15),'steel')

guardrail('Far shoulder rail',-85.5,94.5,-24.7)
guardrail('Near rail west of entry',-85.5,6.5,-13.3)
guardrail('Near rail east of entry',16.5,94.5,-13.3)

def vec(v, keys='xyz'):
    return '{'+', '.join(f'{k}: {n:.7g}' for k,n in zip(keys,v))+'}'
def header(kind,id):
    return f'--- !u!{kind} &{id}\n'
common='  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n'
serialized=[]
for n in nodes:
    id=n['id']; transform=id+1
    components=[transform]+([id+2,id+3] if n['mat'] else [])+([id+4] if n['collision'] else [])
    serialized.append(header(1,id)+'GameObject:\n'+common+'  serializedVersion: 6\n  m_Component:\n'+
        ''.join(f'  - component: {{fileID: {c}}}\n' for c in components)+
        f'  m_Layer: 0\n  m_Name: {n["name"]}\n  m_TagString: {"Floor" if n["floor"] else "Untagged"}\n'+
        '  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 4\n  m_IsActive: 1\n')
    children=[c['id']+1 for c in nodes if c['parent'] is n]
    serialized.append(header(4,transform)+'Transform:\n'+common+f'  m_GameObject: {{fileID: {id}}}\n  serializedVersion: 2\n'+
        f'  m_LocalRotation: {vec(n["rotation"],"xyzw")}\n  m_LocalPosition: {vec(n["position"])}\n  m_LocalScale: {vec(n["scale"])}\n'+
        '  m_ConstrainProportionsScale: 0\n'+('  m_Children:\n'+''.join(f'  - {{fileID: {c}}}\n' for c in children) if children else '  m_Children: []\n')+
        f'  m_Father: {{fileID: {n["parent"]["id"]+1 if n["parent"] else 0}}}\n  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}\n')
    if n['mat']:
        file_id,g,typ=materials[n['mat']]
        serialized.append(header(33,id+2)+'MeshFilter:\n'+common+f'  m_GameObject: {{fileID: {id}}}\n'+
            '  m_Mesh: {fileID: 10202, guid: 0000000000000000e000000000000000, type: 0}\n')
        serialized.append(header(23,id+3)+'MeshRenderer:\n'+common+f'  m_GameObject: {{fileID: {id}}}\n  m_Enabled: 1\n'+
            f'  m_CastShadows: {0 if n["mat"] in ("white","yellow","asphalt","shoulder") else 1}\n  m_ReceiveShadows: 1\n'+
            '  m_DynamicOccludee: 1\n  m_StaticShadowCaster: 0\n  m_MotionVectors: 1\n  m_LightProbeUsage: 1\n  m_ReflectionProbeUsage: 1\n'+
            f'  m_RenderingLayerMask: 1\n  m_RendererPriority: 0\n  m_Materials:\n  - {{fileID: {file_id}, guid: {g}, type: {typ}}}\n'+
            '  m_StaticBatchInfo:\n    firstSubMesh: 0\n    subMeshCount: 0\n  m_StaticBatchRoot: {fileID: 0}\n'+
            '  m_ProbeAnchor: {fileID: 0}\n  m_LightProbeVolumeOverride: {fileID: 0}\n  m_ScaleInLightmap: 1\n  m_ReceiveGI: 1\n'+
            '  m_SortingLayerID: 0\n  m_SortingLayer: 0\n  m_SortingOrder: 0\n  m_AdditionalVertexStreams: {fileID: 0}\n')
    if n['collision']:
        serialized.append(header(65,id+4)+'BoxCollider:\n'+common+f'  m_GameObject: {{fileID: {id}}}\n'+
            '  m_Material: {fileID: 0}\n  m_IncludeLayers:\n    serializedVersion: 2\n    m_Bits: 0\n'+
            '  m_ExcludeLayers:\n    serializedVersion: 2\n    m_Bits: 0\n  m_LayerOverridePriority: 0\n'+
            '  m_IsTrigger: 0\n  m_ProvidesContacts: 0\n  m_Enabled: 1\n  serializedVersion: 3\n'+
            '  m_Size: {x: 1, y: 1, z: 1}\n  m_Center: {x: 0, y: 0, z: 0}\n')

prefab=OUT/'Highway and Parking.prefab'
prefab.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'+''.join(serialized))
meta(prefab,'PrefabImporter')
s=SCENE.read_text()
prefab_guid=guid(prefab.name)
if prefab_guid not in s:
    match=re.search(r'^--- !u!4 &566011301\n.*?(?=^--- !u!)',s,re.M|re.S)
    assert match and 'm_Children: []' in match[0], 'Highway root changed; inspect before updating.'
    block=match[0]
    block=re.sub(r'm_LocalPosition: .*', 'm_LocalPosition: {x: 0, y: 0, z: 0}',block)
    block=re.sub(r'm_LocalScale: .*', 'm_LocalScale: {x: 1, y: 1, z: 1}',block)
    block=block.replace('m_Children: []','m_Children:\n  - {fileID: 910000000001}')
    s=s[:match.start()]+block+s[match.end():]
    addition=f'''--- !u!1001 &910000000000
PrefabInstance:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Modification:
    serializedVersion: 3
    m_TransformParent: {{fileID: 566011301}}
    m_Modifications: []
    m_RemovedComponents: []
    m_RemovedGameObjects: []
    m_AddedGameObjects: []
    m_AddedComponents: []
  m_SourcePrefab: {{fileID: 100100000, guid: {prefab_guid}, type: 3}}
--- !u!4 &910000000001 stripped
Transform:
  m_CorrespondingSourceObject: {{fileID: 100001, guid: {prefab_guid}, type: 3}}
  m_PrefabInstance: {{fileID: 910000000000}}
  m_PrefabAsset: {{fileID: 0}}
'''
    index=s.index('--- !u!1660057539')
    s=s[:index]+addition+s[index:]
    SCENE.write_text(s)

# Machine-readable layout allows independent geometric checks and previews.
layout=[{k:v for k,v in n.items() if k!='parent'} for n in nodes]
(ROOT/'Temp/highway-layout.json').write_text(json.dumps(layout,indent=2))
assert sum(n['name'].startswith('Wheel stop') for n in nodes)==8
assert 11.5-4>=6.5 and 11.5+4<=16.5
assert -11.8+3>-10.459
assert all(all(v>0 for v in n['scale']) for n in nodes)
print(f'Created {sum(bool(n["mat"]) for n in nodes)} low-poly mesh objects, 8 parking bays; connected existing Highway root.')
