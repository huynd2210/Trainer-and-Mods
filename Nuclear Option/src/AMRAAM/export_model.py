import bpy, struct, os, json
from mathutils import Vector
OUT = os.path.dirname(os.path.abspath(__file__))
bpy.ops.wm.open_mainfile(filepath=r'C:\Woodchop\Code\Astra Corner\artifacts\amraam-missile\amraam_inspired.blend')
asset = bpy.data.collections['AIM-120B inspired missile']
deps = bpy.context.evaluated_depsgraph_get()
os.makedirs(os.path.join(OUT,'assets'),exist_ok=True)
# Convert the evaluated model, preserving its materials, and bake the procedural paint.
bpy.ops.object.select_all(action='DESELECT')
copies=[]
for obj in list(asset.all_objects):
    if obj.type not in {'MESH','FONT','CURVE'}: continue
    evaluated=obj.evaluated_get(deps)
    mesh=bpy.data.meshes.new_from_object(evaluated, depsgraph=deps)
    copy=bpy.data.objects.new('Bake '+obj.name,mesh)
    bpy.context.scene.collection.objects.link(copy)
    copy.matrix_world=obj.matrix_world.copy(); copies.append(copy)
    obj.hide_render=True
for obj in copies: obj.select_set(True)
bpy.context.view_layer.objects.active=copies[0]
bpy.ops.object.join()
baked=bpy.context.object
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(island_margin=.003)
bpy.ops.object.mode_set(mode='OBJECT')
paint=bpy.data.images.new('AMRAAM baked paint',width=2048,height=2048)
for material in baked.data.materials:
    node=material.node_tree.nodes.new('ShaderNodeTexImage'); node.image=paint
    material.node_tree.nodes.active=node
scene=bpy.context.scene; scene.render.engine='CYCLES'; scene.cycles.samples=8
scene.render.bake.use_pass_direct=False; scene.render.bake.use_pass_indirect=False
scene.render.bake.use_pass_color=True; scene.render.bake.margin=8
bpy.ops.object.bake(type='DIFFUSE')
paint.filepath_raw=os.path.join(OUT,'assets','amraam-paint.png'); paint.file_format='PNG'; paint.save()
deps=bpy.context.evaluated_depsgraph_get()
groups = {}
for obj in [baked]:
    if obj.type not in {'MESH','FONT','CURVE'}: continue
    evaluated = obj.evaluated_get(deps)
    mesh = evaluated.to_mesh()
    mesh.calc_loop_triangles()
    normal_matrix = obj.matrix_world.to_3x3().inverted().transposed()
    for tri in mesh.loop_triangles:
        mat = mesh.materials[tri.material_index] if len(mesh.materials) else None
        key = mat.name if mat else 'Default'
        if key not in groups:
            bsdf=mat.node_tree.nodes.get('Principled BSDF') if mat else None
            groups[key] = {'color': [1,1,1,1], 'metallic': min(.18,bsdf.inputs['Metallic'].default_value) if bsdf else 0, 'roughness': max(.68,bsdf.inputs['Roughness'].default_value) if bsdf else .7, 'vertices': []}
        for loop_index in reversed(tri.loops):
            v = obj.matrix_world @ mesh.vertices[mesh.loops[loop_index].vertex_index].co
            n = (normal_matrix @ mesh.corner_normals[loop_index].vector).normalized()
            uv=mesh.uv_layers.active.data[loop_index].uv
            groups[key]['vertices'].append((-v.y, v.z, v.x, -n.y, n.z, n.x,uv.x,uv.y))
    evaluated.to_mesh_clear()
os.makedirs(os.path.join(OUT,'assets'),exist_ok=True)
with open(os.path.join(OUT,'assets','amraam.meshbin'),'wb') as f:
    f.write(struct.pack('<ii', 0x414D5232, len(groups)))
    for name,g in groups.items():
        f.write(struct.pack('<6fi', *g['color'],g['metallic'],g['roughness'],len(g['vertices'])))
        for v in g['vertices']: f.write(struct.pack('<8f',*v))
with open(os.path.join(OUT,'evidence','model-export.json'),'w') as f:
    json.dump({k:{'vertices':len(v['vertices']),'color':v['color']} for k,v in groups.items()},f,indent=2)
print('AMRAAM exported',sum(len(g['vertices'])//3 for g in groups.values()),'triangles')
