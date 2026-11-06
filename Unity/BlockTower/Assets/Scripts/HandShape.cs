using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

/// A hand mesh cut into one convex piece per bone, for a hand that is solid as it is drawn
public static class HandShape
{
    /// Each piece (`pieces[i]` its joint, the palm's the wrist; `tips[i]` the fingertip joint on it, or Invalid) as the
    /// skin of `mesh` that moves with it, in that joint's frame: each vertex goes to its heaviest bone (the palm takes
    /// the wrist's, the palm's and the four finger metacarpals'), and a triangle across two pieces goes to both, so
    /// the hulls overlap at the knuckles and leave no gap. `bind`: mesh to each bone's frame; `joints`: each bone's
    /// joint. A piece with under 4 points is null
    public static Mesh[] Cut(Mesh mesh, Matrix4x4[] bind, XRHandJointID[] joints, XRHandJointID[] pieces, XRHandJointID[] tips)
    {
        var vertices = mesh.vertices;
        var weights = mesh.boneWeights;  // the heaviest first
        var triangles = mesh.triangles;
        var owner = new int[vertices.Length];
        for (int v = 0; v < vertices.Length; v++)
        {
            owner[v] = -1;
            var j = joints[weights[v].boneIndex0];
            for (int p = 0; p < pieces.Length && owner[v] < 0; p++)
                if (Owns(pieces[p], tips[p], j))
                    owner[v] = p;
        }
        var hulls = new Mesh[pieces.Length];
        for (int p = 0; p < pieces.Length; p++)
        {
            int b = System.Array.IndexOf(joints, pieces[p]);
            var frame = b >= 0 ? bind[b] : Matrix4x4.identity;
            var points = new List<Vector3>();
            var index = new Dictionary<int, int>();
            var tris = new List<int>();
            for (int t = 0; t < triangles.Length; t += 3)
            {
                if (owner[triangles[t]] != p && owner[triangles[t + 1]] != p && owner[triangles[t + 2]] != p)
                    continue;
                for (int k = 0; k < 3; k++)
                {
                    int v = triangles[t + k];
                    if (!index.TryGetValue(v, out int at))
                    {
                        index[v] = at = points.Count;
                        points.Add(frame.MultiplyPoint3x4(vertices[v]));
                    }
                    tris.Add(at);
                }
            }
            if (points.Count < 4)
                continue;
            hulls[p] = new Mesh();
            hulls[p].SetVertices(points);
            hulls[p].SetTriangles(tris, 0);
        }
        return hulls;
    }

    static bool Owns(XRHandJointID piece, XRHandJointID tip, XRHandJointID j) => piece != XRHandJointID.Wrist
        ? j == piece || (tip != XRHandJointID.Invalid && j == tip)
        :j is XRHandJointID.Wrist or XRHandJointID.Palm or XRHandJointID.IndexMetacarpal or XRHandJointID.MiddleMetacarpal
            or XRHandJointID.RingMetacarpal or XRHandJointID.LittleMetacarpal;
}
