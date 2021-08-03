using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using UnityEngine;
using Rnd = UnityEngine.Random;

/// <summary>
/// On the Subject of Simon Signals
/// Created by JakkOfKlubs and Lumbud84
/// </summary>
public class SimonSignalsModule : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMAudio Audio;
    public KMRuleSeedable RuleSeedable;

    private static int _moduleIdCounter = 1;
    private int _moduleId;
    private bool _moduleSolved = false;

    public KMSelectable clockButton;
    public KMSelectable counterButton;
    public KMSelectable leftButton;
    public KMSelectable rightButton;
    public KMSelectable resetButton;
    public KMSelectable submitButton;

    public MeshRenderer arrow;
    public Texture[] arrowTextures;

    private int r = 0;
    private Coroutine rotateClockwise;

    RotationInfo[][] directionCells;
    int[] colorOffsets;

    struct RotationInfo
    {
        public int Angle;
        public bool Forced;
        public RotationInfo(int angle, bool forced)
        {
            Angle = angle;
            Forced = forced;
        }
    }

    private void Start()
    {
        _moduleId = _moduleIdCounter++;

        int randTexture = Rnd.Range(0, arrowTextures.Length);
        //arrow.material = arrowMat;
        //arrowMat = arrow.sharedMaterial;
        arrow.material.mainTexture = arrowTextures[randTexture];

        //RULE SEED
        var rnd = RuleSeedable.GetRNG();

        int[] offsets = { -4, -3, -2, -1, 0, 1, 2, 3, 4 };

        var directionsThree = new RotationInfo[]
        {
            new RotationInfo(-240, false),
            new RotationInfo(-120, false),
            new RotationInfo(0, false),
            new RotationInfo(120, false),
            new RotationInfo(240, false),
            new RotationInfo(60, true),
            new RotationInfo(180, true),
            new RotationInfo(300, true)
        };
        var directionsFour = new RotationInfo[]
        {
            new RotationInfo(-270, false),
            new RotationInfo(-180, false),
            new RotationInfo(-90, false),
            new RotationInfo(0, false),
            new RotationInfo(90, false),
            new RotationInfo(180, false),
            new RotationInfo(270, false),
            new RotationInfo(45, true),
            new RotationInfo(135, true),
            new RotationInfo(225, true),
            new RotationInfo(315, true)
        };
        var directionsFive = new RotationInfo[]
        {
            new RotationInfo(-288, false),
            new RotationInfo(-216, false),
            new RotationInfo(-144, false),
            new RotationInfo(-72, false),
            new RotationInfo(0, false),
            new RotationInfo(72, false),
            new RotationInfo(144, false),
            new RotationInfo(216, false),
            new RotationInfo(288, false),
            new RotationInfo(0, true),
            new RotationInfo(72, true),
            new RotationInfo(144, true),
            new RotationInfo(216, true),
            new RotationInfo(288, true)
        };
        var directionsSix = new RotationInfo[]
        {
            new RotationInfo(-300, false),
            new RotationInfo(-240, false),
            new RotationInfo(-180, false),
            new RotationInfo(-120, false),
            new RotationInfo(-60, false),
            new RotationInfo(0, false),
            new RotationInfo(60, false),
            new RotationInfo(120, false),
            new RotationInfo(180, false),
            new RotationInfo(240, false),
            new RotationInfo(300, false),
            new RotationInfo(90, true),
            new RotationInfo(150, true),
            new RotationInfo(210, true),
            new RotationInfo(270, true),
            new RotationInfo(330, true),
            new RotationInfo(30, true)
        };

        var directions = new[]
        {
            directionsThree, directionsFour, directionsFive, directionsSix
        };
        
        directionCells = new RotationInfo[4][];

        for (int j = 0; j <= 3; j++)
        {
            var remainingDir = new List<RotationInfo>();
            directionCells[j] = new RotationInfo[80];
            for (var i = 0; i < 80; i++)
            {
                if (remainingDir.Count == 0)
                {
                    remainingDir = directions[j].ToList();
                    rnd.ShuffleFisherYates(remainingDir);
                    rnd.Next(0, 2);
                }
                var ix = rnd.Next(0, remainingDir.Count);
                directionCells[j][i] = remainingDir[ix];
                remainingDir.RemoveAt(ix);
            }
        }

        colorOffsets = new int[24];

        var remainingOff = new List<int>();
        for (var k = 0; k < 24; k++)
        {
            if (remainingOff.Count == 0)
            {
                remainingOff = offsets.ToList();
                rnd.ShuffleFisherYates(remainingOff);
            }
            var jx = rnd.Next(0, remainingOff.Count);
            colorOffsets[k] = remainingOff[jx];
            remainingOff.RemoveAt(jx);
        }

        //RULE SEED END

        clockButton.OnInteract += delegate ()
        {
            rotateClockwise = StartCoroutine(RotateArrow(r, r + 90));
            r += 90;
            return false;
        };
        counterButton.OnInteract += delegate ()
        {
            rotateClockwise = StartCoroutine(RotateArrow(r, r - 90));
            r -= 90;
            return false;
        };
        leftButton.OnInteract += delegate ()
        {
            Debug.LogFormat("left");
            return false;
        };
        rightButton.OnInteract += delegate ()
        {
            Debug.LogFormat("right");
            return false;
        };
        resetButton.OnInteract += delegate ()
        {
            Debug.LogFormat("reset");
            return false;
        };
        submitButton.OnInteract += delegate ()
        {
            Debug.LogFormat("submit");
            return false;
        };
    }

    void GenerateArrows()
    {
        for (int i = 0; i < 6; i++)
        {

        }
    }

    IEnumerator RotateArrow(float start, float end)
    {
        var duration = 0.15f;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            arrow.transform.localEulerAngles = new Vector3(90, Easing.InOutQuad(elapsed, start, end, duration), 0);
            yield return null;
            elapsed += Time.deltaTime;
        }
        arrow.transform.localEulerAngles = new Vector3(90, end, 0);
    }
}
