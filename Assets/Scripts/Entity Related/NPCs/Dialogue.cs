using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.UI;

/* -----------------------------------------------------------
 * Author:
 * Cami Lee
 *
 * Modified By:
 * Chandler Van, Ian Fletcher
 */// --------------------------------------------------------

/* -----------------------------------------------------------
 * Purpose:
 * Handle the Dialogue System for NPCs
 */// --------------------------------------------------------

public class Dialogue : Interactable
{
    public enum DialogueOptions
    {
        PauseGameTime,
        TextBox,
        TriggerEvent
    }

    [Header("Dialogue Settings")]
    public DialogueOptions dialogueOptions;
    public bool pressToStart = true; // whether dialogue starts automatically
    public float charactersPerSecond = 30;
    // If forgetting to toggle this to false was the issue: Flame Chandler 

    [Header("Optional Additions")] 
    PlayableDirector timeline;
    bool timelinePlaying;


    [Header("External Objects")]
    [SerializeField] private TextAsset script;
    TMP_Text dialogueText;
    public GameObject dialogueBackground;

    // Timeline specific (not needed for other functions)
    [SerializeField] TimelinePlayer timelinePlayer;
    [SerializeField] ResetPositionAnimation reset;

    [Header("Preset Options")]
    public string[] characterNames;
    
    // Current Dialogue variables
    string currentLine;
    int currentLineNo;
    string[][] dialogue;
    bool finishedTyping;
    bool isFirst;

    [HideInInspector] public UnityEvent onDialogEnd;
    [HideInInspector] public UnityEvent onDialogStart;
    private Coroutine currentDialogCoroutine;
    
    void Start()
    {
        isFirst = true;
        // Instantiates interactions script 
        if (pressToStart) // dialogue changes with button press
        {
            switch ((int)dialogueOptions)
            {
                case 0: OnInteractionExecuted += PauseGameTime; break;
                case 1: OnInteractionExecuted += TextBox; break;
                case 2: OnFocusEnter += StartTimeline; break;
            }
        }

        // Instantiates dialogue text TMP component
        dialogueText = GetComponentInChildren<TMP_Text>();
        if (dialogueText == null) { Debug.LogWarning("No TMP_Text component found on the child of " + this.gameObject.name); }
        dialogueText.text = "";

        // Initializes current dialogue sequence
        SetDialogScript(script);        
        // Instantitates timeline object
        timeline = GetComponent<PlayableDirector>();

    }

    private void OnDestroy()
    {
        OnFocusEnter = null;
        OnInteractionExecuted = null;
    }

    /// <summary> SetDialogScript changes and Loads a new dialog script </summary>
    public void SetDialogScript(TextAsset newScript)
    {
        if (dialogueBackground != null) { dialogueBackground.SetActive(false); }
        script = newScript;
        dialogue = ReadFile();
        currentLine = dialogue[currentLineNo][0] + ": " + dialogue[currentLineNo][1];
        currentLineNo = 0;
    }

    private void Update()
    {
        if (!pressToStart) // dialogue changes when player is close to object
        {
            if (InRange()) // inside if statement so doesn't run when pressToStart is true
            {
                if(currentDialogCoroutine == null)
                    switch ((int)dialogueOptions)
                    {
                        case 0: PauseGameTime(); break;
                        case 1: TextBox(); break;
                        case 2: Debug.LogError("TriggerEvent cannot be play on awake"); break;
                    }
            }
        }
    }

    private bool InRange()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, 20);

        foreach (Collider hit in hits)
        {
            if (hit.gameObject.tag == "Player") { return true; }
        }

        return false;
    }

    //-- Dialogue Types --//
    void PauseGameTime()
    { 
        Debug.Log("Called PauseGameTime");
        Time.timeScale = 0f;
        StartDialogue();
    }
    void TextBox()
    {
        Debug.Log("Called TextBox");
        StartDialogue();
    }
    void TriggerEvent()
    {
        Debug.Log("Called TriggerEvent");
        StartDialogue();
    }

    //-- Dialogue Controllers --//
    void StartDialogue()
    {
        onDialogStart?.Invoke();
        // Add change dialogue behavior to input system
        InputManager.OnChangeDialogue += ChangeDialogue;
        if (dialogueBackground != null) { dialogueBackground.SetActive(true); }
        if (!finishedTyping) 
        {
            string previousLine = dialogue[currentLineNo][0] + ": " + dialogue[currentLineNo][1];
            StopCoroutine(TypewriterText(previousLine));
        }
        currentDialogCoroutine = StartCoroutine(TypewriterText(currentLine));
    }

    public void ChangeDialogue()
    {
        if (dialogue[currentLineNo+1] == null && currentLine == dialogueText.text) { ExitDialogue(); }
        else if (finishedTyping) // If the typewriter effect has finished
        {
            if (dialogue[currentLineNo + 1][0] == "BREAK") { isFirst = false; StartTimeline(); return; } // if an action
            currentLineNo++;
            currentLine = dialogue[currentLineNo][0] + ": " + dialogue[currentLineNo][1];
            StartCoroutine(TypewriterText(currentLine));
        }
        else // typewriter effect is not finished
        {
            finishedTyping = true;
            dialogueText.text = currentLine;
        }
    }

    void ExitDialogue()
    {
        Time.timeScale = 1f;
        if (dialogueBackground != null) { dialogueBackground.SetActive(false); }

        // remove change dialogue behavior from input system & revert
        InputManager.OnChangeDialogue -= ChangeDialogue;
        currentLineNo = -1;
        currentLine = "";

        dialogueText.text = currentLine;

        onDialogEnd?.Invoke();
    }

    IEnumerator TypewriterText(string line)
    {
        // NOTE: Do not use Time.deltaTime dependent functions
        // as they will not work with a frozen time scale

        float timer = 0;
        float interval = 0.001f * charactersPerSecond;
        string textBuffer = "";
        char[] chars = line.ToCharArray();
        int i = 0;
        finishedTyping = false;

        while (i < chars.Length && !finishedTyping)
        {
            if (timer > 0.01f)
            {
                textBuffer += chars[i];
                dialogueText.text = textBuffer;
                timer = 0;
                i++;
            }
            else
            {
                timer += interval;
                yield return null;
            }
        }

        dialogueText.text = line;
        finishedTyping = true;
    }

    /// <summary>  Takes information from text files and transfers into something the system can read </summary>
    public string[][] ReadFile()
    {
        // split script based on each line
        string scriptText = script.text;
        string[] lines = Regex.Split(scriptText, "\n|\r|\r\n"); 

        string[][] act = new string[1000][];
        int dialogueIndex = 0;
        string currentSpeaker = "";

        foreach (string line in lines)
        {
            if (line.TrimStart().StartsWith('#') || string.IsNullOrWhiteSpace(line)) { continue; } // if is a comment or blank
             
            else if (IsCharacterName(line)) { currentSpeaker = line;} // If is a name

            else if (line == "BREAK") // If needs to stop for timeline
            {
                act[dialogueIndex] = new string[2];
                act[dialogueIndex][0] = line;
                act[dialogueIndex][1] = "";
                dialogueIndex++;
            }

            else if (line == "END") // stop
            {
                return act;
            }

            else
            {
                string finalString = line;

                //finalString = finalString.Replace("\\n", "\n"); // check for line breaks

                act[dialogueIndex] = new string[2];
                act[dialogueIndex][0] = currentSpeaker;
                act[dialogueIndex][1] = finalString;
                dialogueIndex++;
            }
        }
        
        return act;
    }
    private bool IsCharacterName(string text)
    {
        foreach (string name in characterNames)
        {
            if (text == name) { return true; }
        }
        return false;
    }


    public void TimelineIsPaused()
    {
        reset.ResetPosition();
        timelinePlaying = false;
        timeline.Pause();
        timeline.playableGraph.GetRootPlayable(0).SetSpeed(0);
        TriggerEvent();
        InputManager.OnChangeDialogue += ChangeDialogue;
        Time.timeScale = 0;
    }

    public void EndTimeline()
    {
        timelinePlaying = false;
        timeline.Pause();
        Time.timeScale = 1;
        timelinePlayer.EndTimelinePlayer();
    }

    private void StartTimeline()
    {
        Time.timeScale = 1;
        if (!isFirst)
        {
            timelinePlaying = true;
            timeline.Resume();
            InputManager.OnChangeDialogue -= ChangeDialogue;

            currentLineNo += 2;
            if (currentLineNo < dialogue.Length)
            {
                currentLine = dialogue[currentLineNo][0] + ": " + dialogue[currentLineNo][1];
            }
        }
        else
        {
            timelinePlaying = true;
            timelinePlayer.StartTimelinePlayer();
            timeline.Play();
            InputManager.OnChangeDialogue -= ChangeDialogue;
            timeline.playableGraph.GetRootPlayable(0).SetSpeed(1);
        }
    }

}
