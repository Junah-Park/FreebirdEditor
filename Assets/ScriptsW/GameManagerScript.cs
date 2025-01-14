using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.IO;

public class GameManagerScript : MonoBehaviour
{
    public AudioSource audioSource;
    public TimelineController timelineController;
    public List<CreationElement> effectList = new List<CreationElement>();
    public HashSet<CreationElement> effectsPlaying = new HashSet<CreationElement>();
    public Material skyboxMaterial;
    public GameObject player;
    public Oculus.Interaction.ScrubberUI scrubberUI;

    //public TextMeshProUGUI timeText;
    public TMP_Text timeText;

    enum GameMode {
        Edit,
        Preview
    }

    // TODO: Change to Edit
    GameMode gameMode = GameMode.Edit;


    // Visual Effects
    public GameObject fireworkPrefab;
    public GameObject fireCirclePrefab;

    // Audio Effects
    public AudioReverbFilter reverbFilter;
    public AudioEchoFilter echoFilter;

    // Start is called before the first frame update
    void Start()
    {
        StopReverb();
        StopEcho();
    }

    void Update(){
        GameMode previousGameMode = gameMode;

        //readinput when press a , create object with current time and position at cursor
        if (gameMode == GameMode.Edit) {
            if (Input.GetKeyDown("v")) {
                ApplyRandomFireworkVFX();
            }

            if (Input.GetKeyDown("b")) {
                ApplyRandomFireCircleVFX();
            }

            if (Input.GetKeyDown("r")) {
                AppendReverbEffect();
            }

            if (Input.GetKeyDown("e")) {
                AppendEchoEffect();
            }

            if (Input.GetKeyDown("u")) {
                AppendBlueSkyboxEffect();
            }

            if (Input.GetKeyDown("p")) {
                AppendPinkSkyboxEffect();
            }

            if (Input.GetKeyDown("j")) {
                SaveEffectList();
            }

            if (Input.GetKeyDown("l")) {
                LoadEffectList();
            }

            if (Input.GetKeyDown("c")) {
                Reset();
            }
            
        }

        bool enteredPreviewMode = previousGameMode != GameMode.Preview && gameMode == GameMode.Preview;
        if (enteredPreviewMode) {
            timelineController.EnterPreviewMode();
            audioSource.time = 0;
            audioSource.Play();
        }

        bool leftPreviewMode = previousGameMode == GameMode.Preview && gameMode != GameMode.Preview;
        if (leftPreviewMode) {
            timelineController.ExitPreviewMode();
            audioSource.Pause();
        }

        int numSecondsElapsed = Mathf.FloorToInt(audioSource.time);
        int totalSeconds = Mathf.FloorToInt(audioSource.clip.length);

        timeText.text = SerializeSeconds(numSecondsElapsed) + "/" + SerializeSeconds(totalSeconds);

        // Input play/pause
        if (Input.GetKeyDown("space")) {
            if (!audioSource.isPlaying) {
                audioSource.Play();
            } else {
                audioSource.Pause();
            }
        }

        float almostAudioEnd = audioSource.clip.length - 0.5f;

        // ScrubberUI.value goes from -1 to 1. Also goes from 0-1 in ~25 frames.
        if (!Mathf.Approximately(scrubberUI.value, 0))
        {
            float seekTime = audioSource.time + scrubberUI.value * 20f * Time.deltaTime;
            if (0.1 < seekTime && seekTime < almostAudioEnd)
            {
                audioSource.time = seekTime;
            }
        }

        // When up arrow is held down, `Input.GetAxis("Vertical")` goes from 0-1 in ~25 frames.
        if (!Mathf.Approximately(Input.GetAxis("Vertical"), 0)) {
            float seekTime = audioSource.time + Input.GetAxis("Vertical") * 20f * Time.deltaTime;
            if (0.1 < seekTime && seekTime < almostAudioEnd) {
                audioSource.time = seekTime;
            }
        }

        if (audioSource.isPlaying) {
            // Play VFX
            for (int i = 0; i < effectList.Count; i += 1) {
                CreationElement effect = effectList[i];
                if (effect.startTime <= audioSource.time && audioSource.time <= effect.endTime) {
                    if (effect.type == CreationElement.Type.VFX && !effectsPlaying.Contains(effect)) {
                        StartCoroutine(StartThenStopVFX(effect));
                    }
                }
            }

            CreationElement.EffectName skyboxType = CreationElement.EffectName.NeutralSkybox;

            for (int i = 0; i < effectList.Count; i += 1) {
                CreationElement effect = effectList[i];
                if (effect.startTime <= audioSource.time && audioSource.time <= effect.endTime) {
                    if (effect.type == CreationElement.Type.Skybox) {
                        skyboxType = effect.effectName;
                    }
                }
            }

            
            if (skyboxType == CreationElement.EffectName.NeutralSkybox) {
                Color neutralColor = new Color(0.6118f, 0.6118f, 0.6118f, 1f);
                UpdateSkyboxColor(neutralColor);
            } else if (skyboxType == CreationElement.EffectName.BlueSkybox) {
                Color blueColor = new Color(113/255f, 130/255f, 212/255f, 1f);
                UpdateSkyboxColor(blueColor);
            } else if (skyboxType == CreationElement.EffectName.PinkSkybox) {
                Color pinkColor = new Color(202/255f, 123/255f, 212/255f, 1f);
                UpdateSkyboxColor(pinkColor);
            }



            // Play SFX

            if (IsReverbEffectEnabled()) {
                ApplyReverb();
            } else {
                StopReverb();
            }

            if (IsEchoEffectEnabled()) {
                ApplyEcho();
            } else {
                StopEcho();
            }
        }


        // HACK
        // Manually stop the game when audioSource is close enough to the end.
        // This fixes lots of problems.
        //
        // One problem:
        // For some reason, after the audio source finishes playing, the timeline resets to 0. 
        // But, pressing play starts the game from "a little bit before the end"
        if (audioSource.time >= almostAudioEnd) {
            Stop();
        }


    }

    public void Clapped() {
        Vector3 lastClappedPos = GetComponent<Oculus.Interaction.HandClapInteractor>().GetLastClappedPosition();
        Vector3 playerPos = player.transform.position;

        // float scale = 2f;
        float scale = Random.Range(3f, 4f);

        Vector3 effectPosition = (lastClappedPos - playerPos) * scale + playerPos;
        effectPosition = new Vector3(effectPosition.x, lastClappedPos.y, effectPosition.z);
        if (Random.Range(0, 2) == 0) {
            ApplyFireCircleVFX(effectPosition);
        } else {
            ApplyFireworkVFX(effectPosition);
        }
    }

    private string SerializeSeconds(int seconds) {
        int minutes = seconds / 60;
        int secondsLeft = seconds % 60;
        return $"{minutes.ToString().PadLeft(2,'0')}:{secondsLeft.ToString().PadLeft(2,'0')}";
    }

    public void Play() {
        if (!audioSource.isPlaying) {
            audioSource.Play();
        }
    }

    public void Pause() {
        if (audioSource.isPlaying) {
            audioSource.Pause();
        }
    }

    public void Stop() {
        audioSource.time = 0f;
        audioSource.Stop();
    }

    public void Preview() {
        gameMode = GameMode.Preview;
    }

    public void Edit() {
        gameMode = GameMode.Edit;
    }

    void UpdateSkyboxColor(Color color) {
        if (RenderSettings.skybox.HasProperty("_Tint"))
            RenderSettings.skybox.SetColor("_Tint", color);
        else if (RenderSettings.skybox.HasProperty("_SkyTint"))
            RenderSettings.skybox.SetColor("_SkyTint", color);
    }

    bool IsReverbEffectEnabled() {
        for (int i = 0; i < effectList.Count; i += 1) {
            CreationElement effect = effectList[i];

            if (!(effect.startTime <= audioSource.time && audioSource.time <= effect.endTime)) {
                continue;
            }

            if (effect.type == CreationElement.Type.SFX && effect.effectName == CreationElement.EffectName.Reverb) {
                return true;
            }
        }
        return false;
    }

    bool IsEchoEffectEnabled() {
        for (int i = 0; i < effectList.Count; i += 1) {
            CreationElement effect = effectList[i];

            if (!(effect.startTime <= audioSource.time && audioSource.time <= effect.endTime)) {
                continue;
            }

            if (effect.type == CreationElement.Type.SFX && effect.effectName == CreationElement.EffectName.Echo) {
                return true;
            }
        }
        return false;
    }

    IEnumerator StartThenStopVFX(CreationElement effect) {
        effectsPlaying.Add(effect);

        if (effect.effectName == CreationElement.EffectName.Firework) {
            GameObject vfx = Instantiate(fireworkPrefab, effect.position, Quaternion.identity);
            //vfx.GetComponent<ParticleSystem>().startColor = new Color(255, 0, 0, 1);
            float gap = 0.1f;
            yield return new WaitForSeconds(effect.endTime - effect.startTime + gap);
            Destroy(vfx);
        }

        if (effect.effectName == CreationElement.EffectName.FireCircle) {
            GameObject vfx = Instantiate(fireCirclePrefab, effect.position, Quaternion.identity);
            //vfx.GetComponent<ParticleSystem>().startColor = new Color(255, 0, 0, 1);
            float gap = 0.1f;
            yield return new WaitForSeconds(effect.endTime - effect.startTime + gap);
            Destroy(vfx);
        }
        
        effectsPlaying.Remove(effect);
    }

    public void ApplyRandomFireworkVFX() {
        ApplyFireworkVFX(new Vector3(Random.Range(-1f, 1f), Random.Range(1.0f, 1.5f), player.transform.localPosition.z + 1.5f));
    }

    public void ApplyFireworkVFX(Vector3 position) {
        CreationElement element1 = new CreationElement();
        element1.type = CreationElement.Type.VFX;
        element1.effectName = CreationElement.EffectName.Firework;
        element1.position = position;
        element1.startTime = audioSource.time + 0.05f;
        element1.endTime = element1.startTime + 2f;

        timelineController.AddCreationElement(element1);
        effectList.Add(element1);
    }

    public void ApplyRandomFireCircleVFX() {
        ApplyFireCircleVFX(new Vector3(Random.Range(-1f, 1f), Random.Range(1.0f, 1.5f), player.transform.localPosition.z + 1.5f));
    }

    public void ApplyFireCircleVFX(Vector3 position) {
        CreationElement element1 = new CreationElement();
        element1.type = CreationElement.Type.VFX;
        element1.effectName = CreationElement.EffectName.FireCircle;
        element1.position = position;
        element1.startTime = audioSource.time + 0.05f;
        element1.endTime = element1.startTime + 2f;

        timelineController.AddCreationElement(element1);
        effectList.Add(element1);
    }

    public void AppendEchoEffect() {
        CreationElement element1 = new CreationElement();
        element1.type = CreationElement.Type.SFX;
        element1.effectName = CreationElement.EffectName.Echo;
        element1.position = new Vector3(0, 0, 0);
        element1.startTime = audioSource.time + 0.05f;
        element1.endTime = element1.startTime + 1.5f;

        timelineController.AddCreationElement(element1);
        effectList.Add(element1);
    }

    public void AppendReverbEffect() {
        CreationElement element1 = new CreationElement();
        element1.type = CreationElement.Type.SFX;
        element1.effectName = CreationElement.EffectName.Reverb;
        element1.position = new Vector3(0, 0, 0);
        element1.startTime = audioSource.time + 0.05f;
        element1.endTime = element1.startTime + 1.5f;

        timelineController.AddCreationElement(element1);
        effectList.Add(element1);
    }

    public void AppendBlueSkyboxEffect() {
        CreationElement element1 = new CreationElement();
        element1.type = CreationElement.Type.Skybox;
        element1.effectName = CreationElement.EffectName.BlueSkybox;
        element1.position = new Vector3(0, 0, 0);
        element1.startTime = audioSource.time + 0.05f;
        element1.endTime = element1.startTime + 1.5f;

        timelineController.AddCreationElement(element1);
        effectList.Add(element1);
    }


    public void AppendPinkSkyboxEffect() {
        CreationElement element1 = new CreationElement();
        element1.type = CreationElement.Type.Skybox;
        element1.effectName = CreationElement.EffectName.PinkSkybox;
        element1.position = new Vector3(0, 0, 0);
        element1.startTime = audioSource.time + 0.05f;
        element1.endTime = element1.startTime + 1.5f;

        timelineController.AddCreationElement(element1);
        effectList.Add(element1);
    }

    public void ApplyReverb(){
        reverbFilter.enabled = true;

        /*
        filter.decayTime = 4f;
        filter.room = -500;
        filter.roomHF = -500;
        filter.reverbLevel = -500;
        */
    }

    public void StopReverb(){
        reverbFilter.enabled = false;
    }

    public void ApplyEcho(){
        echoFilter.enabled = true;
    }

    public void StopEcho(){
        echoFilter.enabled = false;
    }

    public void Reset(){
        Debug.Log("Hello World");
        timelineController.ClearTimeline();
        effectList = new List<CreationElement>();
    }

    public void SaveEffectList(){
        string json;
        for (int i=0; i<effectList.Count; i++) {
            json = JsonUtility.ToJson(effectList[i]);
            string path = GetFilePath("effectlist" + i.ToString() + ".txt");
            FileStream fileStream = new FileStream(path, FileMode.Create);

            using (StreamWriter writer = new StreamWriter(fileStream))
            {
                writer.Write(json);
                Debug.Log(json);
            }
        }
    }

    public void ClearSave()
    {
        string[] files = Directory.GetFiles(Application.persistentDataPath);

        for (int i=0; i<files.Length; i++) {
            if (File.Exists(files[i])) {
                File.Delete(files[i]);
            }
            else {
                Debug.LogWarning("File not found");
            }
        }
    }

    public void LoadEffectList(){
        string json;
        string[] files = Directory.GetFiles(Application.persistentDataPath);
        effectList = new List<CreationElement>();
        timelineController.ClearTimeline();

        for (int i=0; i<files.Length; i++) {
            if (File.Exists(files[i])) {
                using (StreamReader reader = new StreamReader(files[i]))
                {
                    json = reader.ReadToEnd();
                    effectList.Add(new CreationElement());
                    JsonUtility.FromJsonOverwrite(json, effectList[effectList.Count-1]);
                    timelineController.AddCreationElement(effectList[effectList.Count-1]);                
                }
            }
            else {
                Debug.LogWarning("File not found");
            }
        }
        
        string path = GetFilePath("effectlist.txt");
        
    }
    
    private string GetFilePath(string fileName)
    {
        Debug.Log(Application.persistentDataPath);
        return Application.persistentDataPath + "/" + fileName;
    }
}
