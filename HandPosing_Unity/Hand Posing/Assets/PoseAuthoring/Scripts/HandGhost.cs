﻿using UnityEngine;
using static PoseAuthoring.ScoredSnapPose;

namespace PoseAuthoring
{
    [RequireComponent(typeof(HandPuppet))]
    public class HandGhost : MonoBehaviour
    {
        [SerializeField]
        private Renderer handRenderer;
        [SerializeField]
        private Color highlightedColor = Color.yellow;
        [SerializeField]
        private Color defaultColor = Color.blue;
        [SerializeField]
        private string colorProperty = "_RimColor";


        [InspectorButton("MakeStaticPose")]
        public string StaticPose;
        [InspectorButton("CreateDuplicate")]
        public string Duplicate;

        [SerializeField]
        private VolumetricPose _snapPoseVolume;
        public VolumetricPose SnapPoseVolume
        {
            get
            {
                return _snapPoseVolume;
            }
        }

        private Transform _relativeTo;
        public Transform RelativeTo
        {
            get
            {
                return _relativeTo ?? this.transform.parent;
            }
            private set
            {
                _relativeTo = value;
            }
        }

        private HandPuppet _puppet;
        private HandPuppet Puppet
        {
            get
            {
                if (_puppet == null)
                {
                    _puppet = this.GetComponent<HandPuppet>();
                }
                return _puppet;
            }
        }

        public SnappableObject Snappable { get; private set; }


        private int colorIndex; //TODO external

        private void Awake()
        {
            this.colorIndex = Shader.PropertyToID(colorProperty);
            Highlight(false);
        }

        public void SetPose(HandSnapPose userPose, SnappableObject snappable)
        {
            Puppet.LerpToPose(userPose, snappable.transform);
            RelativeTo = snappable.transform;
            Snappable = snappable;

            _snapPoseVolume = new VolumetricPose()
            {
                pose = userPose,
                volume = new CylinderSurface(Puppet.Grip).MakeSinglePoint(),
                maxDistance = 0.1f
            };
        }

        public void SetPoseVolume(VolumetricPose poseVolume, SnappableObject snappable)
        {
            SetPose(poseVolume.pose, snappable);
            _snapPoseVolume = poseVolume;
            _snapPoseVolume.volume.transform = Puppet.Grip;

        }

        public void RefreshPose(Transform relativeTo)
        {
            _snapPoseVolume.pose = Puppet.VisualPose(relativeTo);
        }

        public void Highlight(float amount)
        {
            if (handRenderer != null)
            {
                Color color = Color.Lerp(defaultColor, highlightedColor, amount);
                handRenderer.material.SetColor(colorIndex, color);
            }
        }

        public void Highlight(bool highlight)
        {
            if (handRenderer != null)
            {
                Color color = highlight ? highlightedColor : defaultColor;
                handRenderer.material.SetColor(colorIndex, color);
            }
        }

        public void MakeStaticPose()
        {
            _snapPoseVolume.volume.MakeSinglePoint();
        }

        public void CreateDuplicate()
        {
            HandGhost ghost = Instantiate(this, this.transform.parent);
            ghost.SetPoseVolume(this._snapPoseVolume, Snappable);
            ghost.transform.SetPositionAndRotation(this.transform.position, this.transform.rotation);
        }

        private void Reset()
        {
            _snapPoseVolume.volume = new CylinderSurface(Puppet.Grip);
            handRenderer = this.GetComponentInChildren<SkinnedMeshRenderer>();
        }

        public ScoredSnapPose CalculateBestPlace(HandSnapPose userPose, float? scoreWeight = null, SnapDirection direction = SnapDirection.Any)
        {
            HandSnapPose snapPose = _snapPoseVolume.pose;

            if (snapPose.handeness != userPose.handeness
                && !_snapPoseVolume.ambydextrous)
            {
                return ScoredSnapPose.Null();
            }

            Pose measuringPoint = userPose.ToPose(RelativeTo);
            scoreWeight = scoreWeight ?? this.Snappable.PositionRotationWeight;

            ScoredSnapPose? bestForwardPose = null;
            ScoredSnapPose? bestBackwardPose = null;

            if (direction == SnapDirection.Any
                || direction == SnapDirection.Forward)
            {
                bestForwardPose = ComparePoses(userPose, snapPose, measuringPoint, scoreWeight.Value, SnapDirection.Forward);
            }

            if (_snapPoseVolume.handCanInvert
                && (direction == SnapDirection.Any
                || direction == SnapDirection.Backward))
            {
                HandSnapPose invertedPose = _snapPoseVolume.InvertedPose(RelativeTo);
                bestBackwardPose = ComparePoses(userPose, invertedPose, measuringPoint, scoreWeight.Value, SnapDirection.Backward);

                if (!bestForwardPose.HasValue
                    || bestBackwardPose.Value.Score > bestForwardPose.Value.Score)
                {
                    return bestBackwardPose.Value;
                }
            }

            return bestForwardPose ?? bestBackwardPose.Value;
        }

        public Vector3 NearestInVolume(Vector3 worldPoint)
        {
            return _snapPoseVolume.volume.NearestPointInSurface(worldPoint);
        }

        private ScoredSnapPose ComparePoses(HandSnapPose userPose, HandSnapPose snapPose, Pose measuringPoint, float scoreWeight, SnapDirection direction)
        {
            var similarPlace = SimilarPlaceAtVolume(userPose, snapPose);
            var nearestPlace = NearestPlaceAtVolume(userPose, snapPose);
            Pose bestForwardPlace = SelectBestPose(similarPlace, nearestPlace, measuringPoint, scoreWeight, out float bestScore);

            return new ScoredSnapPose(AdjustPlace(bestForwardPlace), bestScore, direction);
        }

        private Pose SelectBestPose(Pose a, Pose b, Pose comparer, float normalisedWeight, out float bestScore)
        {
            float aScore = Score(comparer, a);
            float bScore = Score(comparer, b);
            if (aScore * normalisedWeight >= bScore * (1f - normalisedWeight))
            {
                bestScore = aScore;
                return a;
            }
            bestScore = bScore;
            return b;
        }

        private float Score(Pose from, Pose to)
        {
            float forwardDifference = Vector3.Dot(from.rotation * Vector3.forward, to.rotation * Vector3.forward) * 0.5f + 0.5f;
            float upDifference = Vector3.Dot(from.rotation * Vector3.up, to.rotation * Vector3.up) * 0.5f + 0.5f;

            float positionDifference = 1f - Mathf.Clamp01(Vector3.Distance(from.position, to.position) / this.SnapPoseVolume.maxDistance);

            return forwardDifference * upDifference * positionDifference;
        }

        private Pose NearestPlaceAtVolume(HandSnapPose userPose, HandSnapPose snapPose)
        {
            Vector3 desiredPos = RelativeTo.TransformPoint(userPose.relativeGripPos);
            Quaternion baseRot = RelativeTo.rotation * snapPose.relativeGripRot;

            Vector3 surfacePoint = _snapPoseVolume.volume.NearestPointInSurface(desiredPos);
            Quaternion surfaceRotation = _snapPoseVolume.volume.CalculateRotationOffset(surfacePoint, RelativeTo) * baseRot;

            return new Pose(surfacePoint, surfaceRotation);
        }

        private Pose SimilarPlaceAtVolume(HandSnapPose userPose, HandSnapPose snapPose)
        {
            CylinderSurface cylinder = _snapPoseVolume.volume;
            Vector3 desiredPos = RelativeTo.TransformPoint(userPose.relativeGripPos);
            Quaternion baseRot = RelativeTo.rotation * snapPose.relativeGripRot;
            Quaternion desiredRot = RelativeTo.rotation * userPose.relativeGripRot;

            Quaternion rotDif = (desiredRot) * Quaternion.Inverse(baseRot);
            Vector3 desiredDirection = (rotDif * cylinder.Rotation) * Vector3.forward;
            Vector3 projectedDirection = Vector3.ProjectOnPlane(desiredDirection, cylinder.Direction).normalized;

            Vector3 altitudePoint = cylinder.PointAltitude(desiredPos);
            Vector3 surfacePoint = cylinder.NearestPointInSurface(altitudePoint + projectedDirection * cylinder.Radious);
            Quaternion surfaceRotation = cylinder.CalculateRotationOffset(surfacePoint, RelativeTo) * baseRot;

            return new Pose(surfacePoint, surfaceRotation);
        }

        private HandSnapPose AdjustPlace(Pose volumePlace)
        {
            HandSnapPose snapPose = _snapPoseVolume.pose;
            snapPose.relativeGripPos = RelativeTo.InverseTransformPoint(volumePlace.position);
            snapPose.relativeGripRot = Quaternion.Inverse(RelativeTo.rotation) * volumePlace.rotation;
            return snapPose;
        }
    }
}
