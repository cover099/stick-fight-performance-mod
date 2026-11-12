using UnityEngine;

namespace PerfMod
{
    // The game scene has two cameras that render every frame for nothing (measured: +~22% FPS, +~30% 1% lows,
    // worst frame 42 -> 11 ms at 3440x1440 when both are skipped):
    //  - ScreenshotCamera (the level editor's thumbnail camera, under EditorLoadSaveUI) is left active, so it
    //    draws the whole scene, which Main Camera then clears and draws again. Its component is kept off: the
    //    editor's screenshot still works, as it activates the object and calls Camera.Render() itself.
    //  - Camera_Pause draws only the pause menus (MENU layer) but runs every frame, with a full-screen
    //    post-processing pass. It's only needed while paused, or outside a fight (menus, lobby).
    //    Its canvas ("Menu": the whole pause menu, 611 UI elements and 210 texts, all left active) is still
    //    batched by Unity every frame even with the camera off: it's switched off with the camera (+~30% FPS
    //    measured). Only canvases drawn through this camera are touched; it's the only thing that shows them.
    internal class RedundantCameras : MonoBehaviour
    {
        private Camera screenshot;
        private Camera pause;
        private readonly System.Collections.Generic.List<Canvas> pauseCanvases = new System.Collections.Generic.List<Canvas>();
        private float nextCanvasSearch;
        private float nextSearch;

        private void LateUpdate()
        {
            if (!Plugin.RedundantCameras.Value)
            {
                if (pause != null && !pause.enabled)
                {
                    pause.enabled = true;
                }
                foreach (Canvas canvas in pauseCanvases)
                {
                    if (canvas != null && !canvas.enabled)
                    {
                        canvas.enabled = true;
                    }
                }
                return;
            }
            float now = Time.realtimeSinceStartup;
            if ((screenshot == null || pause == null) && now >= nextSearch)
            {
                nextSearch = now + 1f;
                foreach (Camera cam in Resources.FindObjectsOfTypeAll<Camera>())
                {
                    if (!cam.gameObject.scene.IsValid())
                    {
                        continue;
                    }
                    if (cam.name == "ScreenshotCamera")
                    {
                        screenshot = cam;
                    }
                    else if (cam.name == "Camera_Pause")
                    {
                        pause = cam;
                    }
                }
            }
            if (screenshot != null && screenshot.enabled)
            {
                screenshot.enabled = false;
            }
            if (pause != null)
            {
                bool needed = PauseManager.isPaused || !GameManager.inFight;
                if (pause.enabled != needed)
                {
                    pause.enabled = needed;
                }
                if (now >= nextCanvasSearch)
                {
                    // (re)collect: canvases can be created later (scene changes)
                    nextCanvasSearch = now + 2f;
                    pauseCanvases.Clear();
                    foreach (Canvas canvas in Resources.FindObjectsOfTypeAll<Canvas>())
                    {
                        if (canvas.gameObject.scene.IsValid() && canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == pause)
                        {
                            pauseCanvases.Add(canvas);
                        }
                    }
                }
                foreach (Canvas canvas in pauseCanvases)
                {
                    if (canvas != null && canvas.enabled != needed)
                    {
                        canvas.enabled = needed;
                    }
                }
            }
        }
    }
}
