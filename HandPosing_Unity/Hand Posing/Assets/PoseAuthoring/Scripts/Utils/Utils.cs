﻿using UnityEngine;

namespace PoseAuthoring
{
    public static class Utils
    {
        public static void SetPose(this Transform transform, Pose pose, Space space = Space.World)
        {
            if(space == Space.World)
            {
                transform.SetPositionAndRotation(pose.position, pose.rotation);
            }
            else
            {
                transform.localRotation = pose.rotation;
                transform.localPosition = pose.position;
            }
        }

        public static Pose GetPose(this Transform transform, Space space = Space.World)
        {
            if (space == Space.World)
            {
                return new Pose(transform.position, transform.rotation);
            }
            else
            {
                return new Pose(transform.localPosition, transform.localRotation);
            }
        }

        public static Pose RelativeOffset(this Transform to, Transform from)
        {
            return RelativeOffset(from.position, from.rotation, to.position, to.rotation);
        }

        public static Pose RelativeOffset(this Transform to, Pose from)
        {
            return RelativeOffset(from.position, from.rotation, to.position, to.rotation);
        }

        public static Pose RelativeOffset(Pose from, Pose to)
        {
            return RelativeOffset(from.position, from.rotation, to.position, to.rotation);
        }

        public static Pose RelativeOffset(Vector3 fromPosition, Quaternion fromRotation, Vector3 toPosition, Quaternion toRotation)
        {
            Quaternion inverseTo = Quaternion.Inverse(toRotation);
            Vector3 relativePosition = inverseTo * (fromPosition - toPosition);
            Quaternion relativeRotation = inverseTo * fromRotation;

            return new Pose(relativePosition, relativeRotation);
        }

        public static Pose GlobalPose(this Transform reference, Pose offset)
        {
            return new Pose(
                reference.position + reference.rotation * offset.position,
                reference.rotation * offset.rotation);
        }


    }
}