bl_info = {
    "name": "Assets4All",
    "author": "Assets4All",
    "version": (0, 1, 2),
    "blender": (4, 2, 0),
    "location": "View3D > Sidebar > Assets4All",
    "description": "AI 3D asset to game-ready asset with automatic analysis and repair",
    "category": "3D View",
}

from . import operators, properties, ui


def register():
    properties.register()
    operators.register()
    ui.register()


def unregister():
    ui.unregister()
    operators.unregister()
    properties.unregister()
