using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BruteForceFiller : MonoBehaviour
{
    private float _voxelSize = 0.2f;
    private int _voxelOffset = 2;
    private int _triesPerIteration = 25000;
    private int _iterations = 10;

    private int _tryCounter = 0;
    private int _iterationCounter = 0;

    private bool generating = false;
    private int _seed = 0;

    private List<int> test = new List<int>();

    private Dictionary<int, float> _efficiencies = new Dictionary<int, float>();
    private List<int> orderedEfficiencyIndex = new List<int>();
    private BuildingManager _buildingManager;
    private VoxelGrid _grid;

    private List<Voxel> _targetVoxels;
    private List<Voxel> _xzVoxels;
    private List<Voxel> _yzVoxels;
    private List<Voxel> _xyVoxels;

    public BuildingManager BManager
    {
        get
        {
            if (_buildingManager == null)
            {
                GameObject manager = GameObject.Find("Manager");
                _buildingManager = manager.GetComponent<BuildingManager>();
            }
            return _buildingManager;
        }
    }


    /// <summary>
    /// Generate a random index within the voxelgrid
    /// </summary>
    /// <returns>The index</returns>
    Vector3Int RandomIndex()
    {
        int x = Random.Range(0, _grid.GridSize.x);
        int y = Random.Range(0, _grid.GridSize.y);
        int z = Random.Range(0, _grid.GridSize.z);
        return new Vector3Int(x, y, z);
    }

    Vector3Int RandomVoxelXZ()
    {
        int i = Random.Range(0, _xzVoxels.Count);
        return new Vector3Int(_xzVoxels[i].Index.x, _xzVoxels[i].Index.y, _xzVoxels[i].Index.z);
    }

    Vector3Int RandomVoxelYZ()
    {
        int i = Random.Range(0, _yzVoxels.Count);
        return new Vector3Int(_yzVoxels[i].Index.x, _yzVoxels[i].Index.y, _yzVoxels[i].Index.z);
    }

    Vector3Int RandomVoxelOther()
    {
        int i = Random.Range(0, _xyVoxels.Count);
        return new Vector3Int(_xyVoxels[i].Index.x, _xyVoxels[i].Index.y, _xyVoxels[i].Index.z);
    }

    /// <summary>
    /// Get a random rotation alligned with the x,y or z axis
    /// </summary>
    /// <returns>The rotation</returns>
    Quaternion RandomRotation()
    {
        int x = Random.Range(0, 4) * 90;
        int y = Random.Range(0, 4) * 90;
        int z = Random.Range(0, 4) * 90;
        return Quaternion.Euler(x, y, z);
    }
    Quaternion RandomRotationXZ()
    {
        int x = Random.Range(0, 2) * 180;
        int y = Random.Range(0, 4) * 90;
        int z = Random.Range(0, 2) * 180;
        return Quaternion.Euler(x, y, z);
    }

    Quaternion RandomRotationXY()
    {
        int x = Random.Range(0, 2) * 180;
        int y = Random.Range(0, 2) * 180;
        int z = Random.Range(0, 4) * 90;
        return Quaternion.Euler(x, y, z);
    }

    // Start is called before the first frame update
    void Start()
    {
        _grid = BManager.CreateVoxelGrid(BoundingMesh.GetGridDimensions(_voxelOffset, _voxelSize), _voxelSize, BoundingMesh.GetOrigin(_voxelOffset, _voxelSize));
        Debug.Log(_grid.GridSize);
        _grid.DisableOutsideBoundingMesh();
        Random.seed = _seed;

        //get all voxels in the boundingmesh-shaped grid
        //_voxelList = (List<Voxel>)_grid.GetVoxels();
        //a method to find all the voxels with empty neighbours in boundingmesh

        //_targetVoxels = new List<Voxel>();
        //foreach (var voxel in _grid.FlattenedVoxels)
        //{
        //    //if (HasEmptyNeighbourUpward(voxel)) voxel.Status = VoxelState.Dead;
        //    if (HasEmptyNeighbourUpward(voxel))
        //    {
        //        _targetVoxels.Add(voxel);
        //    }
        //}

        _xzVoxels = new List<Voxel>();
        _yzVoxels = new List<Voxel>();
        _xyVoxels = new List<Voxel>();

        //Move to a function in VoxelGrid
        foreach (var voxel in _grid.FlattenedVoxels)
        {
            if (CheckIndices(new int[] { 0, 1 }, voxel)) _xzVoxels.Add(voxel);
            else if (CheckIndices(new int[] { 2, 3 }, voxel)) _yzVoxels.Add(voxel);
            else _xyVoxels.Add(voxel);
        }
    }

    bool CheckIndices(int[] indicesToCheck, Voxel voxel)
    {
        Voxel[] neighbours = voxel.GetNeighbours();
        foreach (var index in indicesToCheck)
        {
            if (neighbours[index] == null || neighbours[index].Status == VoxelState.Dead) return true;
        }
        return false;
    }

    // Update is called once per frame
    void Update()
    {

        if (Input.GetKeyDown("space"))
        {
            //TryAddRandomBlock();

            if (!generating)
            {
                generating = true;

                //StartCoroutine(BruteForce());
                //BruteForceStep();
                StartCoroutine(BruteForceEngine());
            }
            else
            {
                generating = false;
                StopAllCoroutines();
            }
        }
        if (Input.GetKeyDown("r")) _grid.SetRandomType();

    }
    /// OnGUI is used to display all the scripted graphic user interface elements in the Unity loop
    private void OnGUI()
    {
        int padding = 10;
        int labelHeight = 20;
        int labelWidth = 250;
        int counter = 0;

        if (generating)
        {
            _grid.ShowVoxels = GUI.Toggle(new Rect(padding, (padding + labelHeight) * ++counter, labelWidth, labelHeight), _grid.ShowVoxels, "Show voxels");

            GUI.Label(new Rect(padding, (padding + labelHeight) * ++counter, labelWidth, labelHeight),
                $"Grid {_grid.Efficiency} % filled");
            GUI.Label(new Rect(padding, (padding + labelHeight) * ++counter, labelWidth, labelHeight),
                $"Grid {_grid.NumberOfBlocks} Blocks added");
        }
        for (int i = 0; i < Mathf.Min(orderedEfficiencyIndex.Count, 10); i++)
        {
            string text = $"Seed: {orderedEfficiencyIndex[i]} Efficiency: {_efficiencies[orderedEfficiencyIndex[i]]}";
            GUI.Label(new Rect(padding, (padding + labelHeight) * ++counter, labelWidth, labelHeight),
               text);

        }
    }

    /// <summary>
    /// Method to test adding one block to the brid
    /// </summary>
    private void BlockTest()
    {
        var anchor = new Vector3Int(2, 8, 0);
        var rotation = Quaternion.Euler(0, 0, -90);
        _grid.AddBlock(anchor, rotation);
        _grid.TryAddCurrentBlocksToGrid();
    }

    /// <summary>
    /// Method to add a random block to the grid
    /// </summary>
    /// <returns>returns true if it managed to add the block to the grid</returns>
    private bool TryAddRandomBlockXZ()
    {
        //_grid.SetRandomType();
        _grid.SetTypeByRatio();
        _grid.AddBlock(RandomVoxelXZ(), RandomRotationXZ());
        //_grid.AddBlock(RandomVoxelYZ(), RandomRotationXY());
        //_grid.AddBlock(RandomVoxelOther(), RandomRotation());
        bool blockAdded = _grid.TryAddCurrentBlocksToGrid();
        //Debug.Log("HEllo");
        _grid.PurgeUnplacedBlocks();
        return blockAdded;
    }

    private bool TryAddRandomBlockXY()
    {
        //_grid.SetRandomType();
        _grid.SetTypeByRatio();
        //_grid.AddBlock(RandomVoxelXZ(), RandomRotationXZ());
        _grid.AddBlock(RandomVoxelYZ(), RandomRotationXY());
        //_grid.AddBlock(RandomVoxelOther(), RandomRotation());
        bool blockAdded = _grid.TryAddCurrentBlocksToGrid();
        //Debug.Log("HEllo");
        _grid.PurgeUnplacedBlocks();
        return blockAdded;
    }

    /// <summary>
    /// Try adding a random block to the grid every given time. This will run as much times as defined in the _tries field
    /// </summary>
    /// <returns>Wait 0.01 seconds between each iteration</returns>
    IEnumerator BruteForce()
    {
        while (_tryCounter < _triesPerIteration)
        {
            TryAddRandomBlockXZ();
            _tryCounter++;
            yield return new WaitForSeconds(0.01f);
        }
    }

    /// <summary>
    /// Brute force random blocks in the available grid
    /// </summary>
    private void BruteForceStep()
    {
        _grid.PurgeAllBlocks();
        _tryCounter = 0;
        while (_tryCounter < _triesPerIteration)
        {
            TryAddRandomBlockXZ();
            TryAddRandomBlockXY();
            _tryCounter++;
        }

        //Keep track of the most efficient seeds
        _efficiencies.Add(_seed, _grid.Efficiency);
        orderedEfficiencyIndex = _efficiencies.Keys.OrderByDescending(k => _efficiencies[k]).Take(11).ToList();
        if (orderedEfficiencyIndex.Count == 11)
            _efficiencies.Remove(orderedEfficiencyIndex[10]);


    }

    /// <summary>
    /// Brute force an entire iteration every tick
    /// </summary>
    /// <returns></returns>
    IEnumerator BruteForceEngine()
    {
        while (_iterationCounter < _iterations)
        {
            Random.seed = _seed++;
            BruteForceStep();
            _iterationCounter++;
            yield return new WaitForSeconds(0.05f);
        }

        foreach (var value in _efficiencies.Values)
        {
            Debug.Log(value);
        }
    }

    //If you need this, move it into the voxel class
    public bool HasEmptyNeighbourUpward(Voxel voxel)
    {
        Vector3Int upperVoxelIndex = voxel.Index + Vector3Int.up;
        if (!Util.CheckBounds(upperVoxelIndex, _grid)) return true;
        if (_grid.GetVoxelByIndex(upperVoxelIndex).Status == VoxelState.Dead) return true;
        return false;
    }


}
