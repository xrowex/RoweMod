using System;
using rowemod.Mods;
using UnityEngine;
internal static class Program
{
    static int checks;
    static void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; }
    static bool Near(Vector3 a, Vector3 b, float epsilon = .0001f) => (a - b).magnitude < epsilon;
    static void Main()
    {
        foreach (var rootRotation in new[] { Quaternion.identity, Quaternion.Euler(22, 137, -31) })
        {
            var frame = new Transform { localPosition = new Vector3(3, 2, -1), localRotation = rootRotation, scale = 1.3f };
            var cranks = new Transform { parent = frame, localPosition = new Vector3(0, .29f, -.1f) };
            var socketLocal = new Vector3(0, .326f, -.105f);
            var axleLocal = new Vector3(0, .036f, -.005f);
            var socket = frame.TransformPoint(socketLocal); var axle = cranks.TransformPoint(axleLocal);
            var bearing = new StudioBikeBearing(frame, cranks, socket, axle, frame.rotation * Vector3.right, frame.rotation);
            bool connected = true, parallel = true, twist = true;
            foreach (float spin in new[] { 0, 30, 90, 180, 270, 359 })
            {
                cranks.localPosition = new Vector3(0, .29f, -.1f); cranks.localRotation = Quaternion.Euler(spin, 0, 0);
                bearing.Constrain();
                connected &= Near(cranks.TransformPoint(axleLocal), bearing.Pivot);
                parallel &= Vector3.Dot(cranks.rotation * Vector3.right, frame.rotation * Vector3.right) > .9999f;
                twist &= Near(cranks.rotation * Vector3.up, frame.rotation * (Quaternion.Euler(spin, 0, 0) * Vector3.up));
            }
            Check(connected, "equipped BB shaft stays in the frame socket throughout a full crank rotation");
            Check(parallel && twist, "bearing correction preserves spin and axle direction on a rotated/scaled bike");
            cranks.localRotation = Quaternion.Euler(40, 23, 19); bearing.Constrain();
            Check(Near(cranks.TransformPoint(axleLocal), bearing.Pivot) && Vector3.Dot(cranks.rotation * Vector3.right, frame.rotation * Vector3.right) > .9999f, "non-mechanical native tilt is projected onto the equipped axle");
            var beforePosition = cranks.position; var beforeRotation = cranks.rotation;
            StudioBikeBearing.Apply(cranks, bearing.Pivot, bearing.Basis, Vector3.zero, Vector3.zero, 1);
            Check(Near(cranks.position, beforePosition) && Near(cranks.rotation * Vector3.up, beforeRotation * Vector3.up), "an empty edit leaves the constrained native pose unchanged");
            foreach (var edit in new[] { new Vector3(35, 0, 0), new Vector3(0, -25, 0), new Vector3(0, 0, 50) })
            {
                cranks.position = beforePosition; cranks.rotation = beforeRotation;
                StudioBikeBearing.Apply(cranks, bearing.Pivot, bearing.Basis, edit, Vector3.zero, 1);
                Check(Near(cranks.TransformPoint(axleLocal), bearing.Pivot), "every edit axis rotates about the physical part center rather than the offset animation joint");
            }
            cranks.position = beforePosition; cranks.rotation = beforeRotation;
            StudioBikeBearing.Apply(cranks, bearing.Pivot, bearing.Basis, new Vector3(60, 0, 0), Vector3.zero, .5f);
            Check(Near(cranks.rotation * Vector3.up, (bearing.Basis * Quaternion.Euler(30, 0, 0) * Quaternion.Inverse(bearing.Basis) * beforeRotation) * Vector3.up), "weighted playback uses the same physical-pivot rotation as preview");

            var pedal = new Transform { parent = cranks, localPosition = new Vector3(-.118f, 0, -.125f) };
            var pedalPoint = cranks.TransformPoint(pedal.localPosition);
            var pedalBearing = new StudioBikeBearing(cranks, pedal, pedalPoint, pedalPoint, cranks.rotation * Vector3.right, cranks.rotation);
            cranks.localRotation = Quaternion.Euler(145, 0, 0); bearing.Constrain(); pedal.localPosition += new Vector3(0, .08f, -.06f); pedalBearing.Constrain();
            Check(Near(pedal.position, pedalBearing.Pivot), "pedal spindle follows its corrected rotating crank parent");
        }
        var bindings = new (string path, int joint)[] {
            ("Chassis Visuals/Parts/Frame/Mesh",0),("Chassis Visuals/Parts/Seat/Mesh",0),("Chassis Visuals/Parts/Drive Train/Chain/Chain Top",0),
            ("BB Visuals/BB/Bearing/Mesh",0),("BB Visuals/Left Crank Arm/Mesh",2),("BB Visuals/Right Crank Arm/Mesh",2),("BB Visuals/Sprocket/Mesh",2),
            ("Left Pedal Visuals/Left Pedal/Mesh",3),("Right Pedal Visuals/Right Pedal/Mesh",4),
            ("Left Pedal Visuals/Left Pedal/Axle",2),("Right Pedal Visuals/Right Pedal/Axle/Mesh",2),
            ("Back Wheel Visuals/Rotator Bone/Wheel Mesh/Tire",6),("Back Wheel Back/Pegs/Left Peg/Mesh",0),
            ("Front Wheel Front/Wheel Mesh/Spokes",5),("Front Wheel Front/Pegs/Right Peg/Mesh",1),
            ("Front Wheel Front/Offset/Wheel Mesh/Hub",5),("Front Wheel Front/Offset/Pegs/Left Peg/Mesh",1),
            ("Front Wheel Front/Offset/Rotator Bone/Wheel Mesh/Tire",5),
            ("Headset Visuals/Bars/Mesh",1),("Headset Visuals/Stem/Mesh",1),("Front Wheel Visuals/Forks/Mesh",1),
            ("BB Visuals/BB Bolts/Mesh",2),("Unknown/Mesh",-1),("Chassis Visuals Extra/Mesh",-1)
        };
        foreach (var b in bindings) Check(StudioBikePartMap.Joint(b.path) == b.joint, "part classification: " + b.path);
        Console.WriteLine($"PASS: {checks} physical bike pivot, axis, hierarchy and part-mapping checks. Native catalog sweep is separate.");
    }
}
