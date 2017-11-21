﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ObjectLocator : MonoBehaviour {

    [SerializeField] RawImage preview;
    [SerializeField] GameObject marker;

    [SerializeField] GameObject labelPrefab;
    [SerializeField] Transform markerContainer;

    [SerializeField] int boundaryWidth;

    [SerializeField] Color[] colors;

    public int camResolutionWidth;
    public int camResolutionHeight;

    /// <summary>
    /// A hacky way to debug mark boundaries. Stretch to use setpixel32
    /// </summary>
    /// <param name="xMin"></param>
    /// <param name="xMax"></param>
    /// <param name="yMin"></param>
    /// <param name="yMax"></param>
    /// <param name="score"></param>
    /// <returns></returns>
	public IEnumerator DefineBoundary(string type, int xMin, int xMax, int yMin, int yMax, float score)
    {
        DebugManager.Instance.PrintToInfoLog(type + " " + xMin + " " +  xMax + " " +  yMin + " " +  yMax);
        Texture2D tex = preview.texture as Texture2D;
        tex = Object.Instantiate(tex);

        Color selectionColor = colors[Random.Range(0, colors.Length - 1)];

        for (int i = xMin - boundaryWidth; i < (xMax) + boundaryWidth; i++)
        {
            if (i == xMin + boundaryWidth)
                i = xMax - boundaryWidth;

            //Inconsistent pixel(0,0) positioning! Unity starts from bottom left. CNN start from top left.
            for (int j = tex.height - (yMax + boundaryWidth); j < tex.height - (yMin - boundaryWidth); j++)
            {
                tex.SetPixel(i, j, selectionColor);
                if (j == tex.height -( yMax - boundaryWidth))
                    j = tex.height - (yMin + boundaryWidth);
            }
        }
        
        tex.Apply();
        preview.texture = tex;
        yield return new WaitForEndOfFrame();
    }

    /// <summary>
    /// Locate the object position in real world. Calls Dropmarker if hit otherwise ignores
    /// </summary>
    /// <param name="resp">The response structure</param>
    /// <param name="cameraToWorldMatrix">cameraToWorldMatrix</param>
    /// <param name="projectionMatrix">cameraProjectionMatrix</param>
	public void LocateInScene(ResponseStruct resp, Matrix4x4 cameraToWorldMatrix, Matrix4x4 projectionMatrix)
    {
        foreach (ObjectRecognition o in resp.recognizedObjects)
        {
            StartCoroutine(DefineBoundary(o.type, (int) (o.details[0] * camResolutionWidth), (int)(o.details[2] * camResolutionWidth),
                (int)(o.details[1] * camResolutionHeight), (int)(o.details[3] * camResolutionHeight), o.score));
            Vector3? hitPoint = PixelToWorldPoint(new Vector2(o.details[0] + (o.details[2] - o.details[0]) / 2, 
                                        o.details[1] + (o.details[3] - o.details[1]) / 2),
                                cameraToWorldMatrix, projectionMatrix);

            if(hitPoint.HasValue)
                DropMarker(hitPoint.Value, o.type);
            else
                DebugManager.Instance.PrintToRunningLog("No boundary found");
        }
    }

    /// <summary>
    /// This is where the actual magic happens. Calculates the 3D direction where the object sits
    /// </summary>
    /// <param name="pixelPos">pixelPosition as given by CNN. Will be converted to Unity compatible</param>
    /// <param name="cameraToWorldMatrix">cameraToWorldMatrix</param>
    /// <param name="projectionMatrix">projectionMatrix</param>
    /// <returns>Returns a nullable vector3 with position of the hit point if a collider found. Returns null if miss</returns>
    Vector3? PixelToWorldPoint(Vector2 pixelPos, Matrix4x4 cameraToWorldMatrix, Matrix4x4 projectionMatrix)
    {
        //Pixel positions : Unity starts from bottom left. CNN start from top left.
        pixelPos.y = 1 - pixelPos.y;

        Vector3 camPosition = cameraToWorldMatrix.MultiplyPoint(Vector3.zero);

        Vector3 imagePosProjected = ((pixelPos * 2) - Vector2.one); // -1 to 1 space
        imagePosProjected.z = 1;

        Vector3 cameraSpacePos = UnProjectVector(projectionMatrix, imagePosProjected);
        Vector3 worldSpaceRayPoint2 = cameraToWorldMatrix * cameraSpacePos; // ray point in world space

        DebugManager.Instance.PrintToRunningLog("point2:" + worldSpaceRayPoint2);

        return RayCastHitPoint(camPosition, worldSpaceRayPoint2);
        //DrawLineRenderer(camPosition, worldSpaceRayPoint2);
    }

    public static Vector3 UnProjectVector(Matrix4x4 proj, Vector3 to)
    {
        Vector3 from = new Vector3(0, 0, 0);
        var axsX = proj.GetRow(0);
        var axsY = proj.GetRow(1);
        var axsZ = proj.GetRow(2);
        from.z = to.z / axsZ.z;
        from.y = (to.y - (from.z * axsY.z)) / axsY.y;
        from.x = (to.x - (from.z * axsX.z)) / axsX.x;
        return from;
    }

    /// <summary>
    /// Raycast towards the object to find a collider.
    /// </summary>
    /// <param name="origin">from</param>
    /// <param name="direction">direction</param>
    /// <returns>Returns a nullable vector3 with position of the hit point if a collider found. Returns null if miss</returns>
    Vector3? RayCastHitPoint(Vector3 origin, Vector3 direction)
    {
        RaycastHit hit;

        if (Physics.Raycast(origin, direction, out hit, 100))
        {
            DebugManager.Instance.PrintToRunningLog("Found at:" + hit.distance + ":" + hit.point);
            marker.transform.position = hit.point;
            return hit.point;
        }
        else
        {            
            return null;
        }
    }

    public void DropMarker(Vector3 pos, string label)
    {
        GameObject go = GameObject.Instantiate(labelPrefab as Object, markerContainer) as GameObject;
        //GameObject.Instantiate(labelPrefab as Object, pos, Camera.main.transform.rotation, markerContainer) as GameObject;
        go.GetComponent<ObjectLabels>().SetLabel(pos, label);
    }

    void DrawLineRenderer(Vector3 from, Vector3 objPosition)
    {
        LineRenderer line = GetComponent<LineRenderer>();
        line.enabled = true;
        line.SetPosition(0, from);
        line.SetPosition(1, objPosition);
    }
}
