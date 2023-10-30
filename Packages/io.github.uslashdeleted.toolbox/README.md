# Toolbox

The example controller is completely assembled with the provided tools.

**Note:** Ensure that "Write defaults" are enabled; otherwise, the tools will not work.

## Create Int Layer

Animations added in the Create Int Layer are arranged in order. 
Element 0 has an int value of 1, and so on. Please keep this in mind.

## Create Bool Layers

For each animation in the Create Bool Layers, one layer is created. The layer and parameter will have the same name as the animation.

## Subfolder Naming for the Parts Folder

The subfolder naming scheme for the Parts folder should follow the format `#.*`. Please use the same format for your own projects.

### Example of Valid Subfolder Names:
0. default
1. Folder two
2. Folder3
...
255. folder.name

The number in the name of the subfolder is equivalent to the Int value set for the transition to the selected animation(s) in the subfolder.
**Important:** VRChat does not allow an Int value to exceed 255.
