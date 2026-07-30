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
