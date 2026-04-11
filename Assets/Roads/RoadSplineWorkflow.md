# Road Spline Workflow

## Create A New Road

1. Drag `Assets/Prefabs/Roads/RoadSpline.prefab` into the scene.
2. Select the new road object and switch to Unity's Spline tool context.
3. Move the spline knots to shape the road path.
4. Use the knot tangent handles to smooth turns.
5. If the mesh instances need a refresh, open the component menu on `RoadSplineAuthoring` and run `Rebuild Road Instances`.

## Duplicate The Existing Setup

1. Duplicate the example road in the scene if you want to keep the same profile and spacing.
2. Rename it for the area you are building.
3. Reposition or reshape its spline knots.

## Change The Mesh Or Spacing

1. Select the road object.
2. Assign a different `RoadSplineProfile` on `RoadSplineAuthoring` if you create a new profile later.
3. To change spacing or rotate the segment 90 degrees around the spline direction, edit `Assets/Roads/Road01SplineProfile.asset`.

## Notes

- Unity's `SplineContainer` is the authoring path for the road.
- `RoadSplineAuthoring` bends the normalized road segment mesh along the `SplineContainer` path and keeps the renderer/collider in sync.
- The road segment prefab is normalized so `local Y` is the repeat direction and `local Z` is treated as up for spline alignment.
