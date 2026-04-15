using System.Collections.Generic;
using UnityEngine;

namespace Race.Player
{
    internal sealed class RuntimeHumanoidRagdoll
    {
        private sealed class Part
        {
            public Transform Transform;
            public Rigidbody Rigidbody;
            public Collider Collider;
            public CharacterJoint Joint;
            public Rigidbody OriginalConnectedBody;
            public Vector3 InitialLocalPosition;
            public Quaternion InitialLocalRotation;
        }

        private readonly Animator animator;
        private readonly Transform fallbackRoot;
        private readonly List<Part> parts = new List<Part>();
        private bool isBuilt;

        public RuntimeHumanoidRagdoll(Animator animator, Transform fallbackRoot)
        {
            this.animator = animator;
            this.fallbackRoot = fallbackRoot;
        }

        public bool IsAvailable
        {
            get
            {
                EnsureBuilt();
                return parts.Count > 0;
            }
        }

        public void Enable(Vector3 inheritedVelocity, Vector3 impulseDirection, float impulseStrength)
        {
            EnsureBuilt();
            if (parts.Count == 0)
            {
                return;
            }

            Vector3 forceDirection = impulseDirection.sqrMagnitude > 0.0001f
                ? impulseDirection.normalized
                : Vector3.forward;
            Vector3 scatterOrigin = parts[0].Transform != null ? parts[0].Transform.position : Vector3.zero;

            for (int index = 0; index < parts.Count; index++)
            {
                Part part = parts[index];
                if (part.Collider != null)
                {
                    part.Collider.enabled = true;
                }

                if (part.Rigidbody == null)
                {
                    continue;
                }

                part.Rigidbody.isKinematic = false;
                part.Rigidbody.detectCollisions = true;
                part.Rigidbody.linearVelocity = inheritedVelocity;
                part.Rigidbody.angularVelocity = Vector3.zero;

                if (part.Joint != null)
                {
                    part.Joint.connectedBody = null;
                }

                if (impulseStrength <= Mathf.Epsilon)
                {
                    continue;
                }

                Vector3 partPosition = part.Transform != null ? part.Transform.position : scatterOrigin;
                Vector3 outwardDirection = (partPosition - scatterOrigin);
                outwardDirection = Vector3.ProjectOnPlane(outwardDirection, Vector3.up * -1f);
                if (outwardDirection.sqrMagnitude <= 0.0001f)
                {
                    outwardDirection = Random.onUnitSphere;
                }

                outwardDirection.Normalize();
                Vector3 scatterDirection = (forceDirection * 0.55f)
                    + (outwardDirection * 0.85f)
                    + (Vector3.up * 0.35f);

                part.Rigidbody.AddForce(scatterDirection.normalized * impulseStrength, ForceMode.Impulse);
                part.Rigidbody.AddTorque(Random.onUnitSphere * (impulseStrength * 0.45f), ForceMode.Impulse);
            }
        }

        public void Disable()
        {
            if (!isBuilt)
            {
                return;
            }

            for (int index = 0; index < parts.Count; index++)
            {
                Part part = parts[index];
                if (part.Rigidbody != null)
                {
                    part.Rigidbody.linearVelocity = Vector3.zero;
                    part.Rigidbody.angularVelocity = Vector3.zero;
                    part.Rigidbody.detectCollisions = false;
                    part.Rigidbody.isKinematic = true;
                }

                if (part.Collider != null)
                {
                    part.Collider.enabled = false;
                }

                if (part.Joint != null)
                {
                    part.Joint.connectedBody = part.Joint.transform.parent != null
                        ? part.OriginalConnectedBody
                        : null;
                }

                if (part.Transform != null)
                {
                    part.Transform.localPosition = part.InitialLocalPosition;
                    part.Transform.localRotation = part.InitialLocalRotation;
                }
            }
        }

        private void EnsureBuilt()
        {
            if (isBuilt)
            {
                return;
            }

            isBuilt = true;

            if (animator != null && animator.isHuman)
            {
                BuildHumanoidRagdoll();
            }

            if (parts.Count == 0)
            {
                BuildFallbackRagdoll();
            }

            Disable();
        }

        private void BuildHumanoidRagdoll()
        {
            Part hips = TryCreateCapsulePart(HumanBodyBones.Hips, GetFirstBone(HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest), 12f, null);
            if (hips == null)
            {
                return;
            }

            Part torso = TryCreateCapsulePart(GetFirstBone(HumanBodyBones.UpperChest, HumanBodyBones.Chest, HumanBodyBones.Spine), GetFirstBone(HumanBodyBones.Neck, HumanBodyBones.Head), 10f, hips.Rigidbody);
            TryCreateHeadPart(GetFirstBone(HumanBodyBones.Head, HumanBodyBones.Neck), 4f, torso != null ? torso.Rigidbody : hips.Rigidbody);

            Part leftUpperArm = TryCreateCapsulePart(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, 2.5f, torso != null ? torso.Rigidbody : hips.Rigidbody);
            Part leftLowerArm = TryCreateCapsulePart(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, 2f, leftUpperArm != null ? leftUpperArm.Rigidbody : null);
            TryCreateCapsulePart(HumanBodyBones.LeftHand, HumanBodyBones.LastBone, 1f, leftLowerArm != null ? leftLowerArm.Rigidbody : null);

            Part rightUpperArm = TryCreateCapsulePart(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 2.5f, torso != null ? torso.Rigidbody : hips.Rigidbody);
            Part rightLowerArm = TryCreateCapsulePart(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, 2f, rightUpperArm != null ? rightUpperArm.Rigidbody : null);
            TryCreateCapsulePart(HumanBodyBones.RightHand, HumanBodyBones.LastBone, 1f, rightLowerArm != null ? rightLowerArm.Rigidbody : null);

            Part leftUpperLeg = TryCreateCapsulePart(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, 7f, hips.Rigidbody);
            Part leftLowerLeg = TryCreateCapsulePart(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, 5f, leftUpperLeg != null ? leftUpperLeg.Rigidbody : null);
            TryCreateCapsulePart(HumanBodyBones.LeftFoot, HumanBodyBones.LastBone, 2f, leftLowerLeg != null ? leftLowerLeg.Rigidbody : null);

            Part rightUpperLeg = TryCreateCapsulePart(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, 7f, hips.Rigidbody);
            Part rightLowerLeg = TryCreateCapsulePart(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, 5f, rightUpperLeg != null ? rightUpperLeg.Rigidbody : null);
            TryCreateCapsulePart(HumanBodyBones.RightFoot, HumanBodyBones.LastBone, 2f, rightLowerLeg != null ? rightLowerLeg.Rigidbody : null);
        }

        private void BuildFallbackRagdoll()
        {
            Transform target = fallbackRoot != null ? fallbackRoot : (animator != null ? animator.transform : null);
            if (target == null)
            {
                return;
            }

            Rigidbody rigidbody = GetOrAddComponent<Rigidbody>(target.gameObject);
            rigidbody.mass = 20f;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

            CapsuleCollider capsule = GetOrAddComponent<CapsuleCollider>(target.gameObject);
            capsule.direction = 1;
            capsule.center = new Vector3(0f, 0.9f, 0f);
            capsule.radius = 0.24f;
            capsule.height = 1.8f;

            parts.Add(new Part
            {
                Transform = target,
                Rigidbody = rigidbody,
                Collider = capsule,
                InitialLocalPosition = target.localPosition,
                InitialLocalRotation = target.localRotation
            });
        }

        private Part TryCreateCapsulePart(HumanBodyBones bone, HumanBodyBones childBone, float mass, Rigidbody connectedBody)
        {
            if (bone == HumanBodyBones.LastBone)
            {
                return null;
            }

            Transform boneTransform = animator.GetBoneTransform(bone);
            if (boneTransform == null)
            {
                return null;
            }

            Rigidbody rigidbody = GetOrAddComponent<Rigidbody>(boneTransform.gameObject);
            rigidbody.mass = mass;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

            CapsuleCollider capsule = GetOrAddComponent<CapsuleCollider>(boneTransform.gameObject);
            Vector3 localTarget = ResolveLocalColliderTarget(boneTransform, childBone);
            ConfigureCapsule(capsule, localTarget);

            CharacterJoint joint = boneTransform.GetComponent<CharacterJoint>();
            if (connectedBody != null)
            {
                joint = GetOrAddComponent<CharacterJoint>(boneTransform.gameObject);
                joint.connectedBody = connectedBody;
                joint.enablePreprocessing = false;
                joint.enableCollision = false;
            }

            var part = new Part
            {
                Transform = boneTransform,
                Rigidbody = rigidbody,
                Collider = capsule,
                Joint = joint,
                OriginalConnectedBody = joint != null ? joint.connectedBody : null,
                InitialLocalPosition = boneTransform.localPosition,
                InitialLocalRotation = boneTransform.localRotation
            };

            parts.Add(part);
            return part;
        }

        private void TryCreateHeadPart(HumanBodyBones bone, float mass, Rigidbody connectedBody)
        {
            if (bone == HumanBodyBones.LastBone)
            {
                return;
            }

            Transform boneTransform = animator.GetBoneTransform(bone);
            if (boneTransform == null)
            {
                return;
            }

            Rigidbody rigidbody = GetOrAddComponent<Rigidbody>(boneTransform.gameObject);
            rigidbody.mass = mass;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

            SphereCollider sphere = GetOrAddComponent<SphereCollider>(boneTransform.gameObject);
            sphere.center = Vector3.zero;
            sphere.radius = EstimateHeadRadius(boneTransform);

            CharacterJoint joint = boneTransform.GetComponent<CharacterJoint>();
            if (connectedBody != null)
            {
                joint = GetOrAddComponent<CharacterJoint>(boneTransform.gameObject);
                joint.connectedBody = connectedBody;
                joint.enablePreprocessing = false;
                joint.enableCollision = false;
            }

            parts.Add(new Part
            {
                Transform = boneTransform,
                Rigidbody = rigidbody,
                Collider = sphere,
                Joint = joint,
                OriginalConnectedBody = joint != null ? joint.connectedBody : null,
                InitialLocalPosition = boneTransform.localPosition,
                InitialLocalRotation = boneTransform.localRotation
            });
        }

        private HumanBodyBones GetFirstBone(params HumanBodyBones[] bones)
        {
            for (int index = 0; index < bones.Length; index++)
            {
                if (bones[index] != HumanBodyBones.LastBone && animator.GetBoneTransform(bones[index]) != null)
                {
                    return bones[index];
                }
            }

            return HumanBodyBones.LastBone;
        }

        private Vector3 ResolveLocalColliderTarget(Transform boneTransform, HumanBodyBones childBone)
        {
            Transform childTransform = childBone != HumanBodyBones.LastBone ? animator.GetBoneTransform(childBone) : null;
            if (childTransform != null)
            {
                return boneTransform.InverseTransformPoint(childTransform.position);
            }

            if (boneTransform.childCount > 0)
            {
                return boneTransform.InverseTransformPoint(boneTransform.GetChild(0).position);
            }

            return Vector3.up * 0.2f;
        }

        private static void ConfigureCapsule(CapsuleCollider capsule, Vector3 localTarget)
        {
            Vector3 absolute = new Vector3(Mathf.Abs(localTarget.x), Mathf.Abs(localTarget.y), Mathf.Abs(localTarget.z));
            int axis = 1;
            float dominant = absolute.y;
            if (absolute.x > dominant)
            {
                axis = 0;
                dominant = absolute.x;
            }

            if (absolute.z > dominant)
            {
                axis = 2;
                dominant = absolute.z;
            }

            float length = Mathf.Max(dominant, 0.12f);
            float radius = Mathf.Clamp(length * 0.2f, 0.035f, 0.18f);
            capsule.direction = axis;
            capsule.radius = radius;
            capsule.height = Mathf.Max(radius * 2f, length + radius * 2f);
            capsule.center = localTarget * 0.5f;
        }

        private static float EstimateHeadRadius(Transform headTransform)
        {
            float radius = 0.12f;
            for (int index = 0; index < headTransform.childCount; index++)
            {
                float distance = Vector3.Distance(headTransform.position, headTransform.GetChild(index).position) * 0.5f;
                if (distance > Mathf.Epsilon)
                {
                    radius = Mathf.Max(radius, distance);
                }
            }

            return Mathf.Clamp(radius, 0.1f, 0.22f);
        }

        private static T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            if (component == null)
            {
                component = target.AddComponent<T>();
            }

            return component;
        }
    }
}
