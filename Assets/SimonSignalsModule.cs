using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMAudio Audio;
    public KMRuleSeedable RuleSeedable;

    public KMSelectable ClockwiseButton;
    public KMSelectable CounterClockwiseButton;
    public KMSelectable SubmitButton;

    public MeshRenderer Arrow;
    public Texture[] ArrowTextures;
    public MeshRenderer[] Leds;
    public Material LedOn;
    public Material LedOff;
    public TextMesh[] TPLetters;

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
    private int[] _shapeOffsetsPerStage;
    private int[] _colorOffsetsPerStage;

    private RotationInfo[][] _rotationData;
    private readonly Queue<IEnumerator> _animationQueue = new Queue<IEnumerator>();
    private Coroutine _runningAnimationQueue;
    private static readonly int[] _angleOffsets = new int[] { 180, 315, 0, 270 };
    private char[] _tpLetters;

    private enum RotationType
    {
        Relative,
        Absolute
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
        Arrow.gameObject.SetActive(false);

        //RULE SEED
        var rnd = RuleSeedable.GetRNG();
        Debug.LogFormat("[Simon Signals #{0}] Using rule seed: {1}", _moduleId, rnd.Seed);

        var directions = Enumerable.Range(3, 4).Select(n =>
            Enumerable.Range(-n + 1, 2 * n - 2).Select(i => new RotationInfo(RotationType.Relative, i >= 0 ? i + 1 : i))
                .Concat(Enumerable.Range(0, n).Select(i => new RotationInfo(RotationType.Absolute, i)))
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

        _shapeOffsetsPerStage = new int[3];
        _colorOffsetsPerStage = new int[3];
        _shapeOffsetsPerStage[0] = new[] { 2, 0 }[rnd.Next(0, 2)];
        _colorOffsetsPerStage[0] = new[] { 2, 0 }[rnd.Next(0, 2)];
        _shapeOffsetsPerStage[1] = new[] { 0, 1, 3 }[rnd.Next(0, 3)];
        _colorOffsetsPerStage[1] = new[] { 0, 1, 3 }[rnd.Next(0, 3)];
        _shapeOffsetsPerStage[2] = new[] { 0, 1, 4, 3 }[rnd.Next(0, 4)];
        _colorOffsetsPerStage[2] = new[] { 0, 1, 4, 3 }[rnd.Next(0, 4)];

        //RULE SEED END

        ClockwiseButton.OnInteract = ButtonPress(ClockwiseButton, delegate
        {
            var oldRot = angle();
            _currentRotations[_showingArrow] = (_currentRotations[_showingArrow] + 1) % _numRotations[_showingArrow];
            _animationQueue.Enqueue(RotateArrow(oldRot, angle(), texture()));
        });

        CounterClockwiseButton.OnInteract = ButtonPress(CounterClockwiseButton, delegate
        {
            var oldRot = angle();
            _currentRotations[_showingArrow] = (_currentRotations[_showingArrow] + _numRotations[_showingArrow] - 1) % _numRotations[_showingArrow];
            _animationQueue.Enqueue(RotateArrow(oldRot, angle(), texture()));
        });

        SubmitButton.OnInteract = ButtonPress(SubmitButton, delegate
        {
            if (Enumerable.Range(0, _currentStage + 3).All(i => _currentRotations[i] == _expectedRotations[i]))
            {
                SetStage(_currentStage + 1);
                return;
            }
            Module.HandleStrike();
            Debug.LogFormat("[Simon Signals #{0}] {1}", _moduleId, JsonForLogging(isStrike: true));
        });

        SetStage(0);
        _runningAnimationQueue = StartCoroutine(AnimationQueue());

        Module.OnActivate = delegate
        {
            foreach (var letter in TPLetters)
                letter.gameObject.SetActive(TwitchPlaysActive);
            StartCoroutine(FlashArrows());
        };
    }

    private KMSelectable.OnInteractHandler ButtonPress(KMSelectable button, Action action)
    {
        return delegate
        {
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, button.transform);
            button.AddInteractionPunch(button == SubmitButton ? 1 : .5f);
            if (!_moduleSolved)
                action();
            return false;
        };
    }

    private const float _tpLetterDown = .34f;
    private const float _tpLetterUp = .6f;
    private const float _tpLetterLeft = -0.34f;
    private const float _tpLetterRight = 0.34f;
    private IEnumerator MoveLetter(TextMesh letter, bool up, float x)
    {
        var duration = .2f;
        var elapsed = 0f;
        const float z = -.001f;
        while (elapsed < duration)
        {
            letter.transform.localPosition = new Vector3(x, Easing.OutQuad(elapsed, up ? _tpLetterDown : _tpLetterUp, up ? _tpLetterUp : _tpLetterDown, duration), z);
            elapsed += Time.deltaTime;
            yield return null;
        }
        letter.transform.localPosition = new Vector3(x, up ? _tpLetterUp : _tpLetterDown, z);
    }

    private IEnumerator FlashArrows()
    {
        yield return new WaitForSeconds(Rnd.Range(0, 1.3f));
        _tpLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray().Shuffle().Take(5).ToArray();
        var useLetter = 0;

        var letterData = new bool?[][]
        {
            new bool?[] { null, false, true },
            new bool?[] { false, true, false, true },
            new bool?[] { null, true, false, true, false },
            new bool?[] { null, false, true, null, false, true }
        };

        while (!_moduleSolved)
        {
            Arrow.gameObject.SetActive(true);
            Arrow.material.mainTexture = texture();
            Arrow.transform.localEulerAngles = new Vector3(0, 0, angle());
            _runningAnimationQueue = StartCoroutine(AnimationQueue());

            TPLetters[useLetter].text = _tpLetters[_showingArrow].ToString();
            var isLeft = letterData[_numRotations[_showingArrow] - 3][_currentRotations[_showingArrow]];
            var xCoord = isLeft ?? Rnd.Range(0, 2) != 0 ? _tpLetterLeft : _tpLetterRight;
            StartCoroutine(MoveLetter(TPLetters[useLetter], up: false, x: xCoord));

            yield return new WaitForSeconds(1.2f);
            StopCoroutine(_runningAnimationQueue);
            _animationQueue.Clear();

            StartCoroutine(MoveLetter(TPLetters[useLetter], up: true, x: xCoord));
            Arrow.gameObject.SetActive(false);

            _showingArrow = (_showingArrow + 1) % _initialRotations.Length;
            useLetter ^= 1;

            yield return new WaitForSeconds(.1f);
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

        for (var i = 0; i < Leds.Length; i++)
            Leds[i].sharedMaterial = i < _currentStage ? LedOn : LedOff;

        if (_currentStage == 3)
        {
            Module.HandlePass();
            _moduleSolved = true;
            Debug.LogFormat("[Simon Signals #{0}] Module solved.", _moduleId);
            return;
        }

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
            var refShape = _colorsShapes[(i + _shapeOffsetsPerStage[_currentStage]) % numArrows] & 7;
            var refColor = _colorsShapes[(i + _colorOffsetsPerStage[_currentStage]) % numArrows] >> 3;
            var whatToDo = _rotationData[_numRotations[i] - 3][(refColor << 3) | refShape];

            if (whatToDo.RotationType == RotationType.Relative)
                _expectedRotations[i] = (_initialRotations[i] + whatToDo.Amount + _numRotations[i]) % _numRotations[i];
            else // whatToDo.RotationType == RotationType.Absolute
                _expectedRotations[i] = whatToDo.Amount;
        }

        Debug.LogFormat("[Simon Signals #{0}] {1}", _moduleId, JsonForLogging());
    }

    private string JsonForLogging(bool isStrike = false)
    {
        var numArrows = _currentStage + 3;
        var j = new JObject();
        j["stage"] = _currentStage + 1;
        if (isStrike)
            j["strike"] = 1;
        var arr = new JArray();
        for (var i = 0; i < numArrows; i++)
        {
            var aj = new JObject();
            aj["colorshape"] = _colorsShapes[i];
            aj["initial"] = _initialRotations[i];
            aj["num"] = _numRotations[i];
            aj["expected"] = _expectedRotations[i];
            if (isStrike)
                aj["current"] = _currentRotations[i];
            arr.Add(aj);
        }
        j["arrows"] = arr;
        return j.ToString(Formatting.None);
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
        Arrow.material.mainTexture = texture;
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

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} G cw/ccw [rotate the arrow with the letter G clockwise or counter-clockwise] | !{0} G cw/ccw 3 [rotate it 3 steps] | !{0} submit";
    private bool TwitchPlaysActive = false;
#pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string command)
    {
        Match m;
        if ((m = Regex.Match(command, @"^\s*(?<ltr>[A-Z])\s+(?:(?<cw>cw)|ccw)(?:\s+(?<amt>\d))?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).Success)
        {
            var ix = Array.IndexOf(_tpLetters, m.Groups["ltr"].Value.ToUpperInvariant()[0]);
            if (ix == -1)
                yield break;
            var numberOfTimes = 1;
            if (m.Groups["amt"].Success && (!int.TryParse(m.Groups["amt"].Value, out numberOfTimes) || numberOfTimes > 6))
                yield break;
            var button = m.Groups["cw"].Success ? ClockwiseButton : CounterClockwiseButton;

            yield return null;
            for (var i = 0; i < numberOfTimes; i++)
            {
                while (_showingArrow != ix)
                    yield return null;
                button.OnInteract();
                yield return new WaitForSeconds(.2f);
            }
            yield break;
        }

        if (Regex.IsMatch(command, @"^\s*submit\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            SubmitButton.OnInteract();
            yield return new WaitForSeconds(.1f);
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        while (!_moduleSolved)
        {
            yield return true;

            if (_currentRotations[_showingArrow] != _expectedRotations[_showingArrow])
            {
                ClockwiseButton.OnInteract();
                yield return new WaitForSeconds(.1f);
            }

            if (Enumerable.Range(0, _currentRotations.Length).All(i => _currentRotations[i] == _expectedRotations[i]))
            {
                SubmitButton.OnInteract();
                yield return new WaitForSeconds(.1f);
            }
        }
    }
}

