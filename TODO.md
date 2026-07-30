# TODO

🟨 = Being worked on
✅ = Implemented
🟪 = Partially implemented, postponed

Algorithm:
- ⬜ Pre-calculate the chain and the rolls separately from the applying the poses, just like how HVR IK does it.
  - ⬜ Improve how we integrate the variety of transform rotations in 3D models.
- ⬜ Instead of pre-sampling the spline, use a bisection strategy using the segment lengths.
- ⬜ Decide whether or not to measure the length from the spline and then moving the bone (which is what we're currently doing),
  or to strictly use the segment length. The spline may contain local maximas, so the second approach might not easily converge to a solution.
- ⬜ Decide how to handle the spline control points (p1 and p2).
- ✅ Handle the bone roll.
- ⬜ Fix the bone roll twisting 180 degrees when the homing point is at an abnormal angle. The twist propagation strategy should be more graceful to prevent this.
- ⬜ Handle uniform scaling of the system.

Homing:
- ✅ Lerp out of beacons that are too far.
- ⬜ Ignore some beacons that are behind the forward vector.
- ✅ Implement HVRNPSDirectionality to allow entrance in specific directions.
- ⬜ When two terminations are close to each other, prioritize the one that has the most sensible direction in relation to the chain.
- ⬜ Remove the public Beacons array from the chain.

Query:
- ⬜ Finder radius should be computed in local space to account for avatar scale.

Girth:
- ⬜ Encode the girth by distance to account for variable girth
  - ⬜ Figure out if we need two girth radius encoded, one for determining the center of passage, and one for use as animation information.
- ⬜ Have the beacon side receive information about the girth of chains that are passing through them.

JiggleRig integration:
- ✅ Lerp between an actual JiggleRig and our transforms when the beacon is too far.
- ⬜ Consider controlling the stiffness of the JiggleRig up to an intermediate beacon, if it is outside a termination beacon,
  so that the bone chain can be partially affected past a certain bone.
  - Preleminary testing shows that the portion of the bones that are meant to be static are lagging behind, so we may need
    some tighter control over how JiggleRig applies the bone configuration on the bones that are supposed to be static.
- ⬜ Alternatively, consider just implementing a specialized Jiggle system ourselves.

Vixxy integration:
- ⬜ Figure out how a NPS Chain may communicate its signed distance as a measurement to the beacon.
- ⬜ Figure out how the measurements would be handled when multiple NPS Chain are interacting with the same beacon.
- ⬜ Figure out how elements can be toggled on and off based on whether a certain point is going past a termination.
- ⬜ Figure out how elements can be toggled on and off based on whether a certain point is constricted.

Application integration:
- ⬜ Allow Beacons to be retrofitted onto someone else's avatar, without having to reupload the avatar.

Basis Framework integration 🔺:
- ⬜ Split the Query to its own module, if necessary.
- ⬜ Expose Query to Cilbox.
- ⬜ Try running HVRNPSChain entirely inside Cilbox.

-----

## Cilbox integration

NPS is executed on the CPU, so this raises the question of scripting.

There's at least two approaches to build this:
- A) The application owner installs NPS in its entirety in the application, where only NPS can talk to NPS.
- B) The application owner installs only the communication or heavier parts of NPS in the application, where Cilbox can talk to it.
  The chain logic would be done almost entirely within Cilbox.

The approach A has the advantage of being performant and without conflict, but the users cannot modify NPS to improve
its function unless the application itself is modified. That said, those improvements would not require reuploading the avatar (or non-avatar).

The approach B has the advantage of letting the users improve the implementation of NPS for each avatar (or non-avatar) upload,
independently of the pace of the application, at a possible cost of performance.

I like both approaches, and I'd like to enable both approaches.
