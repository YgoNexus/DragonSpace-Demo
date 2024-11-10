﻿using System.Collections.Generic;
using UnityEngine;
using DragonSpace.Grids;

//simple boids implementation to test quadtrees with. Not especially interesting or optimized!
public class GridBoid : MonoBehaviour, IGridElt
{
    #region Grid element implementation
    public float LeftX => pos.x - halfWidth;  //This is a decent argument for using center pt
    public float BottomY => pos.y - halfHeight;
    public int Width { get; private set; }
    public int Height { get; private set; }
    public IGridElt NextElt { get; set; } = null;

    Transform tf;
    public int ID { get; set; }
    public float halfWidth;   
    public float halfHeight;  //TODO: storing this here kinda defeats the point of not storing it in the grid
    #endregion

    public static LooseDoubleGrid grid;
    public static RectInt bounds;
    public static BoidSettingsTemp sets;
    
    public Vector2 pos;
    public Vector2 facing;
    public Vector2 dir;
    public Vector2 avoid;
    public Vector2 align;
    public Vector2 adjoin;
    List<IGridElt> flock;

    private void Awake()
    {
        tf = transform;
        pos = tf.position.ToV2();
        dir = Random.insideUnitCircle.normalized;
        facing = dir;

        Width = (int)tf.localScale.x;
        Height = (int)tf.localScale.z;
        halfWidth = tf.localScale.x / 2;
        halfHeight = tf.localScale.z / 2;
    }

    void Update()
    {
        if (!AvoidBounds())
        {
            UnityEngine.Profiling.Profiler.BeginSample("Find Flockmates");
            FindFlockmates();
            UnityEngine.Profiling.Profiler.EndSample();

            UnityEngine.Profiling.Profiler.BeginSample("Update boid direction");
            DoBoidThing();
            UnityEngine.Profiling.Profiler.EndSample();
        }
        Steer();
        Move();
    }

    void Steer()
    {
        dir += avoid * sets.avoidStrength;
        dir += align.normalized * sets.alignStrength;
        dir += adjoin * sets.adjoinStrength;
        dir.Normalize();

        //This is a bit weird, just doing it this way to be consistent with the other code
        float maxTurn = sets.turnSpeed * 360 * Time.deltaTime;

        float d = Mathf.Clamp(Vector2.SignedAngle(facing, dir), -maxTurn, maxTurn);
        facing = facing.Rotate(d);

        tf.forward = facing.ToV3();
    }

    void Move()
    {
        UnityEngine.Profiling.Profiler.BeginSample("Move");
        float oldX = LeftX;
        float oldY = BottomY;
        pos += facing * (sets.speed * Time.deltaTime);
        tf.position = pos.ToV3();
        grid.Move(this, oldX, oldY, LeftX, BottomY);
        UnityEngine.Profiling.Profiler.EndSample();
    }

    void FindFlockmates()
    {
        int r = sets.radius;
        float x = LeftX + facing.x * 8;
        float y = BottomY + facing.y * 8;
        //adding 2 here so the radius is from the bounding box's edges
        flock = grid.Query(x - r, y - r, x + 2 + r, y + 2 + r, ID); //TODO: variable sizes (not 2)
    }

    protected virtual void DoBoidThing()
    {
        avoid.Set(0, 0);
        align.Set(0, 0);
        adjoin.Set(0, 0);

        for (int i = flock.Count - 1; i >= 0; --i)
        {
            GridBoid nextBoid = (GridBoid)flock[i];

            Vector2 neighbor = pos - nextBoid.pos;
            avoid += neighbor / neighbor.sqrMagnitude;

            align += nextBoid.dir;

            adjoin += nextBoid.pos;
        }

        if (flock.Count > 0)
        {
            adjoin /= flock.Count;
            adjoin -= pos;
        }
    }

    bool AvoidBounds()
    {
        if (pos.x < 0 || pos.x > bounds.width || pos.y < 0 || pos.y > bounds.height)
        {
            dir = new Vector2(bounds.height / 2, bounds.width / 2) - pos;
            return true;
        }
        else
            return false;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 pos3 = pos.ToV3();
        Gizmos.color = Color.red;
        Gizmos.DrawLine(pos3, pos3 + (avoid * 8).ToV3());
        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(pos3, pos3 + (adjoin.normalized * 8).ToV3());
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(pos3, pos3 + (align.normalized * 8).ToV3());

        //show flockmates
        Gizmos.color = Color.yellow;
        int r = sets.radius;
        float x = LeftX + facing.x * 8;
        float y = BottomY + facing.y * 8;
        flock = grid.Query(x - r, y - r, x + 2 + r, y + 2 + r, ID); //TODO: variable sizes (not 2)
        for (int i = 0; i < flock.Count; ++i)
        {
            Gizmos.DrawLine(pos3, ((GridBoid)flock[i]).pos.ToV3());
        }

        //draw detection "radius"
        Gizmos.color = new Color(1, 0, 1, 1f);
        Gizmos.DrawLine(new Vector3(x - r, 0, y - r), new Vector3(x - r, 0, y + 2 + r)); //left
        Gizmos.DrawLine(new Vector3(x - r, 0, y + 2 + r), new Vector3(x + 2 + r, 0, y + 2 + r)); //top
        Gizmos.DrawLine(new Vector3(x + 2 + r, 0, y + 2 + r), new Vector3(x + 2 + r, 0, y - r)); //right
        Gizmos.DrawLine(new Vector3(x + 2 + r, 0, y - r), new Vector3(x - r, 0, y - r)); //bottom

    }

    private void OnDrawGizmos()
    {
        Vector3 pos3 = pos.ToV3();
        Gizmos.color = Color.green;
        Gizmos.DrawLine(pos3, pos3 + (dir.ToV3() * 4));
        Gizmos.color = Color.white;
        Gizmos.DrawLine(pos3, pos3 + (facing * 4).ToV3());
    }
}