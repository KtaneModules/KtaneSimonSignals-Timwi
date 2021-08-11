using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

using Rnd = UnityEngine.Random;

/// <summary>
/// On the Subject of Simon Signals
/// Created by Lumbud84, JakkOfKlubs and Timwi
/// </summary>
public class SimonSignalsModule : MonoBehaviour
{
    [UnityEditor.MenuItem("DoStuff/DoStuff")]
    public static void DoStuff()
    {
        var m = FindObjectOfType<SimonSignalsModule>();
        m.ArrowTextures = m.ArrowTextures.OrderBy(x => x.name).ToArray();
    }

    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMAudio Audio;
    public KMRuleSeedable RuleSeedable;

    public KMSelectable ClockwiseButton;
    public KMSelectable CounterClockwiseButton;
    public KMSelectable SubmitButton;

    public MeshRenderer Arrow;
    public Texture[] ArrowTextures;

    private static int _moduleIdCounter = 1;
    private int _moduleId;
    private bool _moduleSolved;
    private int _currentStage;
    private int[] _numRotations;
    private int[] _initialRotations;
    private int[] _currentRotations;
    private int[] _colorsShapes;
    private int[] _expectedRotations;
    private int _showingArrow;

    private RotationInfo[][] _rotationData;
    private readonly Queue<IEnumerator> _animationQueue = new Queue<IEnumerator>();
    private Coroutine _runningAnimationQueue;
    private static readonly int[] _angleOffsets = new int[] { 180, 315, 0, 270 };
    private static readonly string[] _colorNames = new[] { "red", "green", "blue", "gray" };

    private enum RotationType
    {
        Static,
        Clockwise,
        CounterClockwise
    }

    private struct RotationInfo
    {
        public RotationType RotationType { get; private set; }
        public int Amount { get; private set; }
        public RotationInfo(RotationType rotationType, int amount)
        {
            Amount = amount;
            RotationType = rotationType;
        }
    }

    private void Start()
    {
        _moduleId = _moduleIdCounter++;

        //RULE SEED
        var rnd = RuleSeedable.GetRNG();

        var directions = Enumerable.Range(3, 4).Select(n =>
            Enumerable.Range(-n + 1, 2 * n - 2).Select(i => new RotationInfo(RotationType.Static, i >= 0 ? i + 1 : i))
                .Concat(Enumerable.Range(0, n).Select(i => new RotationInfo(RotationType.Clockwise, i)))
                .Concat(Enumerable.Range(0, n).Select(i => new RotationInfo(RotationType.CounterClockwise, i)))
                .ToArray())
            .ToArray();

        _rotationData = new RotationInfo[4][];

        for (var j = 3; j <= 6; j++)
        {
            var list = new List<RotationInfo>();
            var remainingDir = new List<RotationInfo>();
            for (var i = 0; i < 32; i++)
            {
                if (remainingDir.Count == 0)
                {
                    remainingDir.AddRange(directions[j - 3]);
                    rnd.ShuffleFisherYates(remainingDir);
                }
                var ix = rnd.Next(0, remainingDir.Count);
                list.Add(remainingDir[ix]);
                remainingDir.RemoveAt(ix);
            }
            _rotationData[j - 3] = list.ToArray();
        }
        //RULE SEED END

        ClockwiseButton.OnInteract += delegate ()
        {
            var oldRot = angle();
            _currentRotations[_showingArrow] = (_currentRotations[_showingArrow] + 1) % _numRotations[_showingArrow];
            _animationQueue.Enqueue(RotateArrow(oldRot, angle(), texture()));
            return false;
        };
        CounterClockwiseButton.OnInteract += delegate ()
        {
            var oldRot = angle();
            _currentRotations[_showingArrow] = (_currentRotations[_showingArrow] + _numRotations[_showingArrow] - 1) % _numRotations[_showingArrow];
            _animationQueue.Enqueue(RotateArrow(oldRot, angle(), texture()));
            return false;
        };
        SubmitButton.OnInteract += delegate ()
        {
            Debug.LogFormat("submit");
            return false;
        };

        SetStage(0);
        _runningAnimationQueue = StartCoroutine(AnimationQueue());
        StartCoroutine(FlashArrows());
    }

    private IEnumerator FlashArrows()
    {
        while (!_moduleSolved)
        {
            _runningAnimationQueue = StartCoroutine(AnimationQueue());
            yield return new WaitForSeconds(1.2f);
            StopCoroutine(_runningAnimationQueue);

            _showingArrow = (_showingArrow + 1) % _initialRotations.Length;
            Arrow.gameObject.SetActive(false);
            yield return new WaitForSeconds(.1f);
            Arrow.gameObject.SetActive(true);
            Arrow.sharedMaterial.mainTexture = texture();
            Arrow.transform.localEulerAngles = new Vector3(0, 0, angle());
        }
    }

    private Texture texture()
    {
        return ArrowTextures[(_colorsShapes[_showingArrow] << 1) + (_currentRotations[_showingArrow] == _initialRotations[_showingArrow] ? 0 : 1)];
    }

    private int angle()
    {
        return -_angleOffsets[_numRotations[_showingArrow] - 3] - 360 * _currentRotations[_showingArrow] / _numRotations[_showingArrow];
    }

    private void SetStage(int stage)
    {
        _currentStage = stage;

        if (_currentStage > 0)
        {
            var newNumPositions = new List<int>(_numRotations);
            var newInitialRotations = new List<int>(_currentRotations);
            var newColorsShapes = new List<int>(_colorsShapes);
            var insertionPoint = Rnd.Range(0, 3 + _currentStage);
            newNumPositions.Insert(insertionPoint, Rnd.Range(3, 7));
            newInitialRotations.Insert(insertionPoint, Rnd.Range(0, newNumPositions[insertionPoint]));
            newColorsShapes.Insert(insertionPoint, Rnd.Range(0, 4 * 8));

            _numRotations = newNumPositions.ToArray();
            _initialRotations = newInitialRotations.ToArray();
            _colorsShapes = newColorsShapes.ToArray();
        }
        else
        {
            _numRotations = Enumerable.Range(0, 3).Select(_ => Rnd.Range(3, 7)).ToArray();
            _initialRotations = Enumerable.Range(0, 3).Select(i => Rnd.Range(0, _numRotations[i])).ToArray();
            _colorsShapes = Enumerable.Range(0, 3).Select(i => Rnd.Range(0, 4 * 8)).ToArray();
        }
        _currentRotations = _initialRotations.ToArray();

        var numArrows = _currentStage + 3;
        _expectedRotations = new int[numArrows];
        for (var i = 0; i < _expectedRotations.Length; i++)
        {
            var refIx = (i - _currentStage + numArrows) % numArrows;
            var whatToDo = _rotationData[_numRotations[refIx] - 3][_colorsShapes[i]];
            if (whatToDo.RotationType == RotationType.Static)
                _expectedRotations[i] = (_initialRotations[i] + whatToDo.Amount + _numRotations[i]) % _numRotations[i];
            else if (whatToDo.RotationType == RotationType.Clockwise)
                _expectedRotations[i] = (_initialRotations[i] + (whatToDo.Amount - _initialRotations[refIx] + _numRotations[refIx]) % _numRotations[refIx]) % _numRotations[i];
            else // whatToDo.RotationType == RotationType.CounterClockwise
                _expectedRotations[i] = (_initialRotations[i] - (_initialRotations[refIx] - whatToDo.Amount + _numRotations[refIx]) % _numRotations[refIx] + _numRotations[i]) % _numRotations[i];
        }

        var j = new JObject();
        j["stage"] = _currentStage + 1;
        var arr = new JArray();
        for (var i = 0; i < numArrows; i++)
        {
            var aj = new JObject();
            aj["colorshape"] = _colorsShapes[i];
            aj["initial"] = _initialRotations[i];
            aj["num"] = _numRotations[i];
            aj["expected"] = _expectedRotations[i];
            arr.Add(aj);
        }
        j["arrows"] = arr;

        Debug.LogFormat("[Simon Signals #{0}] {1}", _moduleId, j.ToString(Formatting.None));
    }

    IEnumerator RotateArrow(float startAngle, float endAngle, Texture texture)
    {
        var duration = 0.15f;
        var elapsed = 0f;
        var start = Quaternion.Euler(0, 0, startAngle);
        var end = Quaternion.Euler(0, 0, endAngle);

        while (elapsed < duration)
        {
            Arrow.transform.localRotation = Quaternion.Slerp(start, end, Easing.InOutQuad(elapsed, 0, 1, duration));
            yield return null;
            elapsed += Time.deltaTime;
        }
        Arrow.transform.localRotation = end;
        Arrow.sharedMaterial.mainTexture = texture;
    }

    IEnumerator AnimationQueue()
    {
        while (true)
        {
            if (_animationQueue.Count > 0)
            {
                var item = _animationQueue.Dequeue();
                while (item.MoveNext())
                    yield return item;
            }
            yield return null;
        }
    }
}
