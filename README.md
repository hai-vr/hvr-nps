NPS
=====

**NPS** is a prototype for a mesh deformation control system designed without using shaders,
intended to be used in **native** environments.

Originally, *(D)PS* shaders were used as a hack to overcome environmental limitations
imposed by UGC platforms, in order to exfiltrate positional information through unusual means,
and process it despite having no means of scripting on the CPU.

However, in native environments that give the user a greater amount of control, I believe that
we should question using shaders at all for this purpose.

<img width="965" height="733" alt="Unity_kRBGB6wQ7Y" src="https://github.com/user-attachments/assets/fb91b3d6-64b9-4ae3-8265-9a4dacd0d237" />


NPS attempts to implement a *(D)PS*-like system for use in environments where:
- The users are given appropriate systems to share positional information, and
- That positional information can be directly used by scripts to drive transforms and other object characteristics.

By exploring a native implementation, we hope that:
- There would not be a need for a build-time overhead induced by complex shaders,
- Graphical artifacts normally caused by shaders would not happen, such as depth and shadow artifacts,
- Users may be able attach objects to the transforms such as particle systems,
- Mixing behaviour with bone jiggle systems would be done via script,
- Creation of custom behaviour may be possible via script modification rather than shader modification.

## Current setup

### Model requirements

The model must be a Skinned Mesh, which armature contains at least one series of bones that are all perfectly lined up in any axis.

Please make sure that the model transform, the Armature transform, and its bones all have a scale of 1, so the model needs to be
exported properly.

The orientation does not matter, and the direction system used by the bones do not matter.
That said, for simplicity, I recommend the model to be placed horizontally rather than vertically, as it makes it more obvious
what the upright direction is.

The density of bones is up to experimentation.

Note: Building a Unity Editor script that converts an existing model to this format should be quite easy, so an editor script may
be made available at a later time.

### HVR NPS Chain

The **HVR NPS Chain** component represents a chain of bones that will attempt to pass through beacons found by its *HVR NPS Finder* component.

The position and orientation of the *HVR NPS Chain* determines how the chain behaves.
- The forward direction (blue arrow) is the direction of the chain, from the root to the tip.
- The up direction (green arrow) should "point up", relative to the forward direction. It is generally the same as the scene up direction,
  when the model is set up horizontally.

The *HVR NPS Chain* should be placed at the position of the root bone; not the position of the element at index 0.

It has the following properties:
- **Finder**: The **HVR NPS Finder** which is used to find the beacons that this chain will attempt to pass through.
- **Elements**: The elements of the chain which will be rotated. Do **not** include the root bone, as the root bone generally should not be rotated.
- **Idle Proxies**: An alternate chain that is a copy of the same bones as those in *Elements*, but that alternate chain can be referenced by a jiggle bone system such as *JiggleRig*.
- **Girth Radius**: The girth radius of the mesh of this chain, or in other words, around half of its thickness.
- **Tip Length**: The extra length beyond the last item defined in *Elements* that still covers the mesh.
- **Beacons**: Used for debugging, this will be removed.

### HVR NPS Finder

The **HVR NPS Finder** component represents an entity that is looking for beacons around its radius.

It has the following properties:
- **Radius**: The radius to look for beacons. The actual radius is computed in local space to account for a change in avatar scale.

### HVR NPS Beacon

The **HVR NPS Beacon** component represents a position that is being broadcast.

It has the following properties:
- **Passage**:
  - *Termination*: This is an end point. Beacons that are found at a further distance to this beacon will not be used.
    - This does not mean that the mesh will be hidden beyond this point; that would be the role of *Constriction*, explained below.
  - *Intermediate*: This a point of passage, possibly leading to another beacon further away.
  - *Internal*: This a point that cannot be found by a finder, to be used in the *Next* array of beacons.
- **Alignment**:
  - *Center*: This beacon is positioned at the center. Chains would go through the center.
  - *Edge*: This beacon is position at an edge. Chains would go through a point located away from the edge in the Up direction
    of the beacon transform, using the girth radius of the chain.
- **Constriction**:
  - *Default*: If the passage is a *Termination* and *Next* is empty, then this is the same as *Constrict To Hide*. Otherwise, it is the same as *No Change*.
  - *No Change*: The chain does not change appearance past this point.
  - *Constrict To Hide*: The chain constricts past this point, in order to hide that chain.
- **Directionality**:
  - *Default*: If the passage is a *Termination*, then this is the same as *One Way*. Otherwise, it is the same as *Two Way*.
  - *Two Way*: The passage can be used in both the Forward and the Backward directions of the beacon transform. 
  - *One Way*: The passage can be used in the Forward direction of the beacon transform.
  - *Reverse Way*: The passage can be used in the Backward direction of the beacon transform. This value is intended to be used by scripts, so that rotating the transform is not necessary.
  - *Along Normal Plane*: The passage can be used in any direction planar to the Up direction of the beacon transform. This is used to represent a flat surface.
- **Next**: An array of zero, one, or several *HVR NPS Beacon* components, ideally of type *Internal*. Any chain that passes through the beacon
  of this component will also try to pass through these beacons in the order defined by the array.