import bpy
import json
import mathutils
import os

INPUT_mat_name = "<<<MAT_NAME>>>"
INPUT_export_path = "<<<EXPORT_PATH>>>"
INPUT_shader_type = "<<<SHADER_TYPE>>>"

shader_metadata = json.load(open(f'{INPUT_export_path}\\Materials\\{INPUT_mat_name}_meta.json'))

# key = name (str), value = most recent node output
variable_dict = {}
texture_dict = {}


def get_tex_name(tex_index):
    return shader_metadata[INPUT_shader_type]["indices"][str(int(tex_index))]


def get_texture(tex_index):
    texname = get_tex_name(tex_index)
    texmeta = get_tex_meta(tex_index)
    # loads existing data if already
    img = bpy.data.images.load(INPUT_export_path + "/Textures/" + f"/{texname}{shader_metadata['format']}",
                               check_existing=True)
    img.alpha_mode = "CHANNEL_PACKED"
    if texmeta['srgb']:
        img.colorspace_settings.name = "sRGB"
    else:
        img.colorspace_settings.name = "Non-Color"
    return img


# this will start crying and screaming if you get anything wrong, so don't get anything wrong
# or else
def get_tex_meta(tex_index):
    texname = get_tex_name(tex_index)
    return shader_metadata['textures'][texname]

def create_cbuffer(cbuffer, name):
    cgroup = bpy.data.node_groups.new(name, 'ShaderNodeTree')
    link = cgroup.links.new
    
    group_in = cgroup.nodes.new('NodeGroupInput')
    group_in.location = (-200,0)
    cgroup.inputs.new('NodeSocketFloat','Index')
    
    group_out = cgroup.nodes.new('NodeGroupOutput')
    group_out.location = (400,0)
    cgroup.outputs.new('NodeSocketColor','Color')
    cgroup.outputs.new('NodeSocketFloat','Alpha')
    
    divider = cgroup.nodes.new('ShaderNodeMath')
    divider.operation = 'DIVIDE'
    divider.location = (0,0)
    link(group_in.outputs[0], divider.inputs[0])
    
    ramp = cgroup.nodes.new("ShaderNodeValToRGB")
    ramp.interpolation = 'CONSTANT'
    
    for i in range(len(cbuffer)):
        ramp.elements.new(i / len(cbuffer))
        ramp.elements[i] = cbuffer[i]
    
    link(divider.outputs[0], ramp.inputs[0])
    
    link(ramp.outputs[0], group_out.inputs[0])
    link(ramp.outputs[1], group_out.inputs[1])
    
    return cgroup

def assemble_mat():
    material = bpy.data.materials.new(name=INPUT_mat_name)
    material.use_nodes = True
    matnodes = material.node_tree.nodes
    link = material.node_tree.links.new

    def update_variable(variable, new_node, input_idx):
        link(variable_dict[variable], new_node.inputs[input_idx])
        variable_dict[variable] = new_node

    # cbuffer helpers
    i = 0

    def register_float(var_name, x):
        nonlocal i
        varNode = matnodes.new("ShaderNodeValue")
        varNode.location = (-370.0, 200.0 + (float(i) * -1.1) * 50)
        varNode.label = var_name
        varNode.outputs[0].default_value = x
        varNode.hide = True
        i += 1
        # print(var_name)
        variable_dict[var_name] = varNode.outputs[0]

    def add_dynamic_cbuffer(index_ref, cbuffer):
        divider = matnodes.new('ShaderNodeMath')
        divider.operation = 'DIVIDE'
        divider.location = (0,0)
        link(index_ref, divider.inputs[0])
        
        ramp = matnodes.new("ShaderNodeValToRGB")
        ramp.interpolation = 'CONSTANT'
        
        for i in range(len(cbuffer)):
            ramp.elements.new(i / len(cbuffer))
            ramp.elements[i] = cbuffer[i]        
        link(divider.outputs[0], ramp.inputs[0])
        
    def add_float4(var_name, x, y, z, w):
        register_float(var_name + ".x", x)
        register_float(var_name + ".y", y)
        register_float(var_name + ".z", z)
        register_float(var_name + ".w", w)

    def add_float3(var_name, x, y, z):
        register_float(var_name + ".x", x)
        register_float(var_name + ".y", y)
        register_float(var_name + ".z", z)

    def add_float2(var_name, x, y):
        register_float(var_name + ".x", x)
        register_float(var_name + ".y", y)

    def add_float(var_name, x):
        register_float(var_name + ".x", x)

    principled_node = matnodes.get('Principled BSDF')

    # <<<REPLACE WITH SCRIPT>>>
    
    if INPUT_shader_type == "pixelShader":
        if True:  # Base Color (Albedo)
            combineRGB = matnodes.new("ShaderNodeCombineColor")
            link(variable_dict['o0.x'], combineRGB.inputs[0])
            link(variable_dict['o0.y'], combineRGB.inputs[1])
            link(variable_dict['o0.z'], combineRGB.inputs[2])
            variable_dict['output_rgb_albedo'] = combineRGB.outputs[0]
            print("rgb")
            link(combineRGB.outputs[0], principled_node.inputs[0])
            link(combineRGB.outputs[0], principled_node.inputs[19])

        if True:  # Normal
            # Biased Normal
            o1Node = matnodes.new("ShaderNodeCombineXYZ")
            link(variable_dict['o1.x'], o1Node.inputs[0])
            link(variable_dict['o1.y'], o1Node.inputs[1])
            link(variable_dict['o1.z'], o1Node.inputs[2])

            biasedNormalNode = matnodes.new("ShaderNodeVectorMath")
            biasedNormalNode.operation = 'SUBTRACT'
            link(o1Node.outputs[0], biasedNormalNode.inputs[0])
            biasedNormalNode.inputs[1].default_value = 0.5, 0.5, 0.5

            # World-Space Normal
            biasedNormalLengthNode = matnodes.new("ShaderNodeVectorMath")
            biasedNormalLengthNode.operation = 'LENGTH'
            link(biasedNormalNode.outputs[0], biasedNormalLengthNode.inputs[0])
        
            invert_biasedNormalLengthNode = matnodes.new("ShaderNodeMath")
            invert_biasedNormalLengthNode.operation = 'DIVIDE'
            invert_biasedNormalLengthNode.inputs[0].default_value = 1
            link(biasedNormalLengthNode.outputs[0], invert_biasedNormalLengthNode.inputs[1])
        
            worldSpaceNormalNode = matnodes.new("ShaderNodeVectorMath")
            worldSpaceNormalNode.operation = 'SCALE'
            link(biasedNormalNode.outputs[0], worldSpaceNormalNode.inputs[0])
            link(invert_biasedNormalLengthNode.outputs[0], worldSpaceNormalNode.inputs[1])

            # Adjust Individual Values
            separateNormalNode = matnodes.new("ShaderNodeSeparateXYZ")
            link(worldSpaceNormalNode.outputs[0], separateNormalNode.inputs[0])

            normalYNode = matnodes.new("ShaderNodeMath")
            normalYNode.operation = 'SUBTRACT'
            normalYNode.inputs[0].default_value = 1
            link(separateNormalNode.outputs[1], normalYNode.inputs[1])

            normalXYNode = matnodes.new('ShaderNodeCombineXYZ')
            link(separateNormalNode.outputs[0], normalXYNode.inputs[0])
            link(separateNormalNode.outputs[1], normalXYNode.inputs[1])

            normalZDotProductNode = matnodes.new("ShaderNodeVectorMath")
            normalZDotProductNode.operation = 'DOT_PRODUCT'
            link(normalXYNode.outputs[0], normalZDotProductNode.inputs[0])
            link(normalXYNode.outputs[0], normalZDotProductNode.inputs[1])

            normalZSaturateNode = matnodes.new("ShaderNodeClamp")
            link(normalZDotProductNode.outputs[0], normalZSaturateNode.inputs[0])

            normalZSubtractNode = matnodes.new("ShaderNodeMath")
            normalZSubtractNode.operation = 'SUBTRACT'
            normalZSubtractNode.inputs[0].default_value = 1
            link(normalZSaturateNode.outputs[0], normalZSubtractNode.inputs[1])

            normalZNode = matnodes.new("ShaderNodeMath")
            normalZNode.operation = 'SQRT'
            link(normalZSubtractNode.outputs[0], normalZNode.inputs[0])

            # Final Assembly
            finalNormalNode = matnodes.new("ShaderNodeCombineXYZ")
            link(separateNormalNode.outputs[0], finalNormalNode.inputs[0])
            link(normalYNode.outputs[0], finalNormalNode.inputs[1])
            link(normalZNode.outputs[0], finalNormalNode.inputs[2])

            link(finalNormalNode.outputs[0], principled_node.inputs[22])
            print("Normal")

        if True:  # Roughness
            smoothness_subtract = matnodes.new("ShaderNodeMath")
            smoothness_subtract.operation = 'SUBTRACT'
            link(variable_dict['o2.y'], smoothness_subtract.inputs[0])
            smoothness_subtract.inputs[1].default_value = 0.375

            smoothness_multiply = matnodes.new("ShaderNodeMath")
            smoothness_multiply.operation = 'MULTIPLY'
            link(smoothness_subtract.outputs[0], smoothness_multiply.inputs[0])
            smoothness_multiply.inputs[1].default_value = 8

            rough_saturate = matnodes.new("ShaderNodeClamp")
            # existing defaults are fine
            link(smoothness_multiply.outputs[0], rough_saturate.inputs[0])

            smoothness_invert = matnodes.new("ShaderNodeMath")
            smoothness_invert.operation = 'SUBTRACT'
            link(rough_saturate.outputs[0], smoothness_invert.inputs[1])
            smoothness_invert.inputs[0].default_value = 1
            print("smoothness")
            link(smoothness_invert.outputs[0], principled_node.inputs[9])

        if True:  # RT2
            link(variable_dict['o2.x'], principled_node.inputs[6])

            emissive_subtract = matnodes.new("ShaderNodeMath")
            emissive_subtract.operation = 'SUBTRACT'
            link(variable_dict['o2.y'], emissive_subtract.inputs[1])
            emissive_subtract.inputs[1].default_value = 0.5

            emissive_multiply = matnodes.new("ShaderNodeMath")
            emissive_multiply.operation = 'MULTIPLY'
            link(emissive_subtract.outputs[0], emissive_multiply.inputs[0])
            emissive_multiply.inputs[1].default_value = 10

            emissive_scale = matnodes.new("ShaderNodeVectorMath")
            emissive_scale.operation = 'SCALE'
            link(variable_dict['output_rgb_albedo'], emissive_scale.inputs[0])
            link(emissive_multiply.outputs[0], emissive_scale.inputs[1])
            print("emissive")
            link(emissive_scale.outputs[0], principled_node.inputs[19])

            # AO is ignored since blender does that on its own


def setup_engine():
    bpy.context.scene.render.engine = 'BLENDER_EEVEE'
    bpy.context.scene.eevee.use_gtao = True


def show_message_box(message="", title="Message Box", icon='INFO'):
    def draw(self, context):
        self.layout.label(text=message)

    bpy.context.window_manager.popup_menu(draw, title=title, icon=icon)


def start_import():
    # Shows a message box with a message, custom title, and a specific icon
    show_message_box(f"Importing Material {INPUT_mat_name}",
                     "This might take some time! (Especially on multiple imports)", 'ERROR')

    # To give the message box a chance to show up
    setup_engine()
    bpy.app.timers.register(assemble_mat, first_interval=0.3)

    # Deselect all objects just in case
    bpy.ops.object.select_all(action='DESELECT')


if __name__ == "__main__":
    start_import()
    if INPUT_shader_type == "vertexShader":
        import PS_<<<MAT_NAME>>> as ps
        #TODO: Generate dict of output variables and provide as input to PS
        #TODO: Run PS from VS