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
