/*
 *  Project Oxalis.
 *  OxalisScript.cs - Used in all applications visualising TotalSim data, controls all functionality within the program. 
 *                  
 *                   Author: Tom Weaver (tweaver@totalsim.co.uk) 2016
 * 
 *                   No license is given for the program outside of its regular use in TotalSim visualisation.  
 */

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using System.IO;


//This class is essentially a struct, but easier to work with because c# and unity. It holds all the data that each slice-axis will need.
public class SliceAxis{
    public string           slname; 
    public int              CurrentSlice, SliceCount, AxisID;
    public bool             isActive, isPlaying, isPaused;
    public List<GameObject> slices;
    public GameObject       Parent;
    //Simple constructor for if the "new" keyword is used.
    public SliceAxis(string n, int cs, int sc, bool ia, int id){
        slname          = n;
        CurrentSlice    = cs;
        SliceCount      = sc;
        isActive        = ia;
        isPlaying       = false;
        isPaused        = true;
        slices          = new List<GameObject>();
        AxisID          = id;
        Parent          = new GameObject();
        Parent.name     = slname+".Parent";
    }
}

public class OxalisScript : MonoBehaviour {

    // PRIVATE VARIABLES
    private bool            mouseRMB, mouseLMB, mouseMMB, showProgress, orDraw, menuopen, autohidemenu, showError, FinishedLoading, showBackgroundProgress, slicesLoaded;
    private Bounds          bounds;
    private string          assetPath, file, slicePath, preFile, currLoad;
    private float           panSpeed, zoomSens, rotSens, fps, delta, camScale, prog, menuHide, startingZoom, smoothing, bgProg;
    private int             pbx, pby;
    private Vector3         startPos, menuOpenPos;
    private Quaternion      startRot;
    private List<string>    Errors;
    // PRIVATE OBJECTS
    private GameObject      CurrentObject, TempGO, NewRotationPoint;
    private List<GameObject> Assets         = new List<GameObject>();
    private List<string>    DropDownOptions = new List<string>();
    private Light[]         AllLights;
    private InputField      ModelXPosIField, ModelYPosIField, ModelZPosIField, ModelXRotIField, ModelYRotIField, ModelZRotIField, 
                            CamXPosIField, CamYPosIField, CamZPosIField, LightBrightnessIField, SliceInput;
    private WWW             Www;
    private Slider          RotSens, ZoomSens, SliceSlider;
    private Toggle          OrientationDraw, AutoHideMenu, LitUnlit, ShowSlices;
    private Texture2D       Empty, Fill;
    //private Canvas          OrientationCanvas;
    private Button          ClearNewRotation, OpenMenuButton, slice_Back, slice_First, slice_Forward, slice_Last, slice_Pause, slice_Play;
    private Material        Lit, Unlit, Unshaded, cullOff;   
    //  PUBLIC OBJECTS
    public Text             uiText, fpsText;
    public Camera           Cam;
    public Dropdown         MeshSelect, ShaderSelect;
    public GameObject       NewMenu, Orientation, SliceUIPanel, SliceParent;
    public RectTransform    NewMenuActual;
    // Slice UI 
    private SliceAxis       sl_X, sl_Y, sl_Z, current;
    private Dropdown        SliceAxisSelector;
    //  PUBLIC VARIABLES

    /// --- UNITY DEFINED METHODS BEGIN
        // Start() - called upon program start. Initiialise any variables needed and call any methods needed for when the 
        // application begins.
	    void Start () {

            //Instantiate all the objects and variables necessary at program start.
            panSpeed                = 1.5f;
            zoomSens                = 1f;
            rotSens                 = 150f;
            camScale                = 2f;
            Cam.orthographicSize    = startingZoom = camScale;
            startPos                = new Vector3(0f, 0f, 5f);
            startRot                = Quaternion.identity;
            mouseRMB                = mouseLMB = mouseMMB = showProgress = false;
            orDraw                  = showError = menuopen = false;
            fps                     = delta = .0f;         
            TempGO                  = new GameObject(); TempGO.name = "tempUsedInRotation"; TempGO.SetActive(false);
            file                    = "";
            prog                    = 0f;
            bgProg                  = 0f;
            menuOpenPos             = Vector3.zero;
            menuHide                = 210f;
            Errors                  = new List<string>();
            Lit                     = new Material(Shader.Find("Vertex Color Lit"));    // Simple shader that reads in the vertex colour attributes of the imported meshes
            Unlit                   = new Material(Shader.Find("vclitnbf"));                 // Simple shader that uses the vertex color attributes but does not incorporate lighting, thus is unlit
            Unshaded                = new Material(Shader.Find("Diffuse"));            // Simple shader incorporating no colour at all, useful for viewing just geometry, colour set using the below code. 
            Unshaded.SetColor("_Color", Color.grey);                                    // Set the colour of the above shader
            cullOff                 = new Material(Shader.Find("vclitnbf"));             // Simple unlit shader that utilises the vertex colour attribute, has no lighting, and turns off back face culling (useful for the slices)
            smoothing               = 20f;
            FinishedLoading         = false;
            showBackgroundProgress  = false;
            preFile                 = "file:///";
            slicesLoaded            = false;
            //Check if there are any command line arguments. Currently the only argument utilised is the assetpath. 
            CheckCommandLineArgs();

            //Start the coroutines associated with downlaoding the asset bundles. 
            StartCoroutine(DLABScheduler());    

            // Setup the UI.
            SetupUI();
        
            //Set up and render the orientation axis in the bottom left corner of the viewport. 
            if(orDraw)
                SetupDrawOrientation();

            //Instantiate the loading menu vars
            LoadingMenu();

            //Call methods and create objects pertaining to the sliceUI
            SliceUI();
            sl_X                    = new SliceAxis("sl_X", 0,10, false, 0);
            Reset(sl_X.Parent);
            sl_Y                    = new SliceAxis("sl_Y", 0, 10, false, 1);
            Reset(sl_Y.Parent);
            sl_Z                    = new SliceAxis("sl_Z", 0, 10, false, 2);
            Reset(sl_Z.Parent);
            current                 = sl_X; //initially set the current axis to X
            sl_X.isActive           = true;
	}

        // Update() - called once per frame, any dynamic objects that need to update or be updated, place code in here.
	    void Update () {
            if(CurrentObject!= null){
	            Interaction();
                UpdateUIElements();
            }

            {//calculate fps
                delta += (Time.deltaTime - delta) * .1f;
                fps = 1.0f / delta;
            }

            if(showProgress){
                prog = (float)Www.progress;
            }

            if(showBackgroundProgress)
                bgProg = (float)Www.progress;

            if(orDraw)
                Orientation.SetActive(true);
            else
                Orientation.SetActive(false);


            if(FinishedLoading && slicesLoaded){
                if(current.Parent != null && CurrentObject != null){
                    current.Parent.transform.position = CurrentObject.transform.position;
                    current.Parent.transform.rotation = CurrentObject.transform.rotation;
                }
            }

	    }
    /// --- UNITY DEFINED METHODS END

    /// --- USER DEFINED METHODS BEGIN
        
        // AdjustSlices() - Adjust the positions of the loaded slices to correspond with the mesh, as the mesh position is also 
        // changed from what is imported to fit the viewport. Uses the bounds set during the mesh adjustment as a basis for locale. 
        private void AdjustSlices(){
            {   //  x
                for(int i = 0; i < sl_X.slices.Count; i++){
                    float mx=0, my=0; 
                        if( sl_X.slices[i].transform.childCount > 0 ){
                            foreach ( Transform gchild in sl_X.slices[i].transform ){
                                mx = gchild.transform.position.x - bounds.center.x;
                                my = gchild.transform.position.y - bounds.center.y;
                                gchild.position = new Vector3(mx, my, 5);

                            }
                        } else {
                            mx = sl_X.slices[i].transform.position.x - bounds.center.x;
                            my = sl_X.slices[i].transform.position.y - bounds.center.y;
                            sl_X.slices[i].transform.position = new Vector3(mx, my, 5);
                        }
                }
            }//endx

            {   //  y
                for(int i = 0; i < sl_Y.slices.Count; i++){
                    float mx=0, my=0; 
                        if( sl_Y.slices[i].transform.childCount > 0 ){
                            foreach ( Transform gchild in sl_Y.slices[i].transform ){
                                mx = gchild.transform.position.x - bounds.center.x;
                                my = gchild.transform.position.y - bounds.center.y;
                                gchild.position = new Vector3(mx, my, 5);

                            }
                        } else {
                            mx = sl_Y.slices[i].transform.position.x - bounds.center.x;
                            my = sl_Y.slices[i].transform.position.y - bounds.center.y;
                            sl_Y.slices[i].transform.position = new Vector3(mx, my, 5);
                        }
                }
            }//endy
            {   //  z
                for(int i = 0; i < sl_Z.slices.Count; i++){
                    float mx=0, my=0; 
                        if( sl_Z.slices[i].transform.childCount > 0 ){
                            foreach ( Transform gchild in sl_Z.slices[i].transform ){
                                mx = gchild.transform.position.x - bounds.center.x;
                                my = gchild.transform.position.y - bounds.center.y;
                                gchild.position = new Vector3(mx, my, 5);

                            }
                        } else {
                            mx = sl_Z.slices[i].transform.position.x - bounds.center.x;
                            my = sl_Z.slices[i].transform.position.y - bounds.center.y;
                            sl_Z.slices[i].transform.position = new Vector3(mx, my, 5);
                        }
                }
            }//endz


        }

        // CalculateBounds() - Check whether the passed GO has any children, and then loops over all children
        // (if any) and encapsulates the bounds to give a relative height, width and depth.
        private void CalculateBounds(GameObject po){
            //Since the mesh splits due to high vert count loop over all children
            //& grandchildren and then sum up their bounds to get the overall model
            //length, depth and height... For science!
            // 01/08 Added logic to ensure that all cases were covered, e.g. when there are no grandchildren!
            bounds = new Bounds(po.transform.position, Vector3.zero);
            Vector3 t = Vector3.zero;
            foreach (Transform child in po.transform){
                if(child.childCount > 0){
                    foreach(Transform grandChild in child.transform){
                        if(grandChild.gameObject.GetComponent<Renderer>() != null){
                            //Renderer ren = grandChild.gameObject.GetComponent<Renderer>();
                            bounds.Encapsulate(grandChild.gameObject.GetComponent<Renderer>().bounds);
                        }
                    }
                }else{
                    //Renderer ren = child.gameObject.GetComponent<Renderer>();
                    bounds.Encapsulate(child.gameObject.GetComponent<Renderer>().bounds);
                }
            }

            foreach(Transform child in po.transform){
                if(child.childCount > 0){
                    foreach(Transform gChild in child.transform){
                        float moveX = gChild.transform.position.x - bounds.center.x;
                        float moveY = gChild.transform.position.y - bounds.center.y;
                        float moveZ = gChild.transform.position.z - bounds.center.z;
                        t = new Vector3(moveX, moveY, moveZ);
                        gChild.transform.position = t;
                    }
                }else{
                    float moveX = child.transform.position.x - bounds.center.x;
                    float moveY = child.transform.position.y - bounds.center.y;
                    float moveZ = child.transform.position.z - bounds.center.z;
                    t = new Vector3(moveX, moveY, moveZ);
                    child.transform.position = t;
                }
            }
            foreach(Renderer r in po.GetComponentsInChildren<Renderer>()){
                r.gameObject.AddComponent<BoxCollider>();
                r.gameObject.GetComponent<BoxCollider>().isTrigger = true;
            }        
        }//CalculateBounds()
        
        // CheckCommandLineArgs() - Check if the user has entered any command line arguments when calling the built application. 
        // this would be used for redefining the path to the asset bundles and such, and allows extra functionality to be added
        // later on should any other needs arise. 
        private void CheckCommandLineArgs(){
            string[] argv = System.Environment.GetCommandLineArgs();
            foreach(string str in argv){
                if(str.Contains("assetpath=")){
                    assetPath = preFile + str.Substring(str.IndexOf("=") + 1);
                }else{
                    assetPath = preFile + "E:/DropBox/EngD/Development/Unity/DATA/AB/";
                }

                if(str.Contains("slices=")){
                    slicePath = str.Substring(str.IndexOf("=") + 1);
                } else {
                    slicePath = "E:/DropBox/EngD/Development/Unity/DATA/AB/slices/";
                }
            }
            
        }
     
        // ClearCreatedRotatePoint() - After a new rotation point is added to the model, this method clears this rotation 
        // point and returns the application to regular rotation.
        private void ClearCreatedRotatePoint(){
            CurrentObject.transform.SetParent(null);
            Destroy(NewRotationPoint);
        }

        // Interaction() - handle all interaction with the simulation by the user, from mouse and keyboard 
        // only at present, but can be extended to include touch screen input too. 
        private void Interaction(){
            //Control the zoom in the scene, lerping between values to provide a smooth transition
            camScale -= Input.GetAxis("Mouse ScrollWheel") * zoomSens;
            camScale = Mathf.Clamp(camScale, 0.01f, 3.0f);
            Cam.orthographicSize = Mathf.Lerp(Cam.orthographicSize, camScale, smoothing * Time.deltaTime);            
            

            
           // if(!ShowSlices.isOn){ //This loop is active if the slices are NOT showing

                { /* PAN | TRANSLATION */
                    //If right mouse button is held down then pan the camera vertically or horizontally
                    if(Input.GetMouseButtonDown(1) && !EventSystem.current.IsPointerOverGameObject()){ mouseRMB = true; }
                    else if(Input.GetMouseButtonUp(1)){ mouseRMB = false;  uiText.text  = "Press R to reset View."; }
                    if ( Input.GetKey(KeyCode.X) && mouseRMB ){
                        TranslateMesh("x");  
                        uiText.text = "Movement along X-Axis Only.";
                    }else if ( Input.GetKey(KeyCode.Y) && mouseRMB ){
                        TranslateMesh("y");  
                        uiText.text = "Movement along Y-Axis Only.";
                    }else if (Input.GetKey(KeyCode.Z) && mouseRMB){
                        TranslateMesh("z");
                        uiText.text = "Movement along Z-Axis Only.";
                    }else if ( mouseRMB && ( !Input.GetKey(KeyCode.X) || !Input.GetKey(KeyCode.Y) || !Input.GetKey(KeyCode.Z) ) ){
                        TranslateMesh("xyz");
                        uiText.text = "Free Movement.";
                    }

                    //current.Parent.transform.position = CurrentObject.transform.position;
                }

                { /* ROTATION  */
                    //if middle mouse button one is held down then rotate model using mouse as input (inverted (the proper way))
                    //holding x or y will only perform the rotation on the axis of the held down key.
                    //Transform temp = CurrentObject.transform; float Smoothing = 10f;
                    if(Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject()){    mouseLMB = true;}
                    else if (Input.GetMouseButtonUp(0)){    mouseLMB = false; uiText.text  = "Press R to reset View.";}
                    if(NewRotationPoint == null){
                        if ( Input.GetKey(KeyCode.Y) && mouseLMB ){
                            TempGO.transform.Rotate(-Vector3.up, Input.GetAxis("Mouse X") * rotSens * Time.deltaTime, Space.Self);
                            uiText.text = "Rotation around Y-axis Only.";
                        }else if ( Input.GetKey(KeyCode.X) && mouseLMB ){
                            TempGO.transform.Rotate(Vector3.right, Input.GetAxis("Mouse Y") * rotSens * Time.deltaTime, Space.Self);
                            uiText.text = "Rotation around X-axis Only.";
                        }else if ( Input.GetKey(KeyCode.Z) && mouseLMB){
                            TempGO.transform.Rotate(-Vector3.forward, Input.GetAxis("Mouse Y") * rotSens * Time.deltaTime, Space.Self);
                            uiText.text = "Rotation around Z-Axis Only";
                        }else if ( mouseLMB ) {
                            TempGO.transform.Rotate(( Input.GetAxis("Mouse Y") * rotSens * camScale * Time.deltaTime), 
                                                    (-Input.GetAxis("Mouse X") * rotSens * camScale * Time.deltaTime), 
                                                    0f, Space.World);
                            uiText.text = "Free Rotation";    
                            
                            ////Also update the slice rotation even if they aren't currently showing. 
                            //current.Parent.transform.Rotate(( Input.GetAxis("Mouse Y") * rotSens * camScale * Time.deltaTime), 
                            //          (-Input.GetAxis("Mouse X") * rotSens * camScale * Time.deltaTime), 
                            //            0f, Space.World);
                        }
                    }else if (mouseLMB && NewRotationPoint != null){
                        TempGO.transform.Rotate(( Input.GetAxis("Mouse Y") * rotSens * camScale * Time.deltaTime), 
                                              (-Input.GetAxis("Mouse X") * rotSens * camScale * Time.deltaTime), 
                                                0f, Space.World);
                        uiText.text = "Rotating around ("+NewRotationPoint.transform.position.x+","+NewRotationPoint.transform.position.y+","+NewRotationPoint.transform.position.z+")";

                    }
                
                    if(Input.GetMouseButtonDown(2) && !EventSystem.current.IsPointerOverGameObject()){  mouseMMB = true;}
                    else if (Input.GetMouseButtonUp(2)){ mouseMMB = false; }

                    if( mouseMMB ){
                        if(NewRotationPoint == null)
                            NewRotationPoint = new GameObject();
                        else{
                            Destroy(NewRotationPoint);
                            NewRotationPoint = new GameObject();
                        }
                        RaycastHit rayhit;
                        Vector3 mouseCoordAtClick = Input.mousePosition;
                        Ray ray = Cam.ScreenPointToRay(mouseCoordAtClick);  
                    
                        NewRotationPoint.transform.rotation = CurrentObject.transform.rotation;
                        NewRotationPoint.name = "NewRotPoint";
                        NewRotationPoint.transform.position = Cam.ScreenToWorldPoint(mouseCoordAtClick);
                        Vector3 forward = transform.TransformDirection(Vector3.forward * 10);
                        Debug.DrawRay(NewRotationPoint.transform.position, forward, Color.red, 3600);
                        if(Physics.Raycast ( ray, out rayhit ) )
                            NewRotationPoint.transform.position = rayhit.point;
                        CurrentObject.transform.SetParent(NewRotationPoint.transform);
                    }
                }

            //To reset camera rotation and position
            if(Input.GetKeyDown(KeyCode.R)){    ResetMesh(); sl_X.Parent.transform.rotation = sl_Y.Parent.transform.rotation = sl_Z.Parent.transform.rotation = CurrentObject.transform.rotation;} 

            //Check if the cursor has moved over the menu whilst the user has been interacting, if so then
            //set whatever mouse button bool it was to false. Rectifying a bug that became apparent 07/11/16
            if      ( mouseLMB && EventSystem.current.IsPointerOverGameObject() )
                mouseLMB = false;
            else if ( mouseRMB && EventSystem.current.IsPointerOverGameObject() )
                mouseRMB = false;
            else if ( mouseMMB && EventSystem.current.IsPointerOverGameObject() )
                mouseMMB = false;


            //After we have worked out changes to the rotation matrix in the interaction methods above, apply them to the actual 
            //object, this bug took me a while to rectify, but now it seems to be working ok ;)
            if ( NewRotationPoint == null ){
                CurrentObject.transform.rotation = Quaternion.Lerp(   CurrentObject.transform.rotation, 
                                                                TempGO.transform.rotation, 
                                                                smoothing * Time.deltaTime);
            } else {
                NewRotationPoint.transform.rotation = Quaternion.Lerp(  NewRotationPoint.transform.rotation, 
                                                                        TempGO.transform.rotation, 
                                                                        smoothing * Time.deltaTime);
            }


            //Update the orientation axes along with the updated rotation matrix of the object
            if( orDraw )
                UpdateDrawOrientation(CurrentObject.transform.rotation);
        }

        // LoadingMenu() - Take care of instantiating variables and displaying the loading menu to the screen.
        private void LoadingMenu(){
            pbx = 300; pby = 30;
            Empty = new Texture2D(pbx, pby);
            Fill = new Texture2D(pbx, pby);
            for(int i = 0; i < pbx; i++){
                for(int j = 0; j < pby; j++){
                    Empty.SetPixel(i, j, Color.black);
                    Fill.SetPixel(i, j, Color.grey);
                }
            }
            Empty.Apply();
            Fill.Apply();
        }

        // ResetMesh() - reset the orientation and position of the currently active object via keypress.
        private void ResetMesh(){
            if(NewRotationPoint != null){
                ClearCreatedRotatePoint();
                CurrentObject.transform.position  = startPos;
                CurrentObject.transform.rotation  = startRot;
            }
            else{
                CurrentObject.transform.position  = startPos;
                CurrentObject.transform.rotation  = startRot;
            }
            TempGO.transform.rotation     = startRot;
            Cam.orthographicSize        = camScale = startingZoom;
            Cam.transform.position      = new Vector3 (0f, 0f, -3f);

            if(orDraw)
                UpdateDrawOrientation(startRot);
            StartCoroutine(ShowMessage("View Reset", 1f));
        }
        
        // Reset(GameObject) - Reset the orientation of the passed GameObject to default values. 
        private void Reset(GameObject go){
            go.transform.rotation   = Quaternion.identity;
            go.transform.position   = new Vector3(0, 0, 5);
            go.transform.localScale = new Vector3(1, 1, 1);
        }

        // RotateMesh() - Rotate mesh around given axis by a certain angle, interpolating using lerp
        // for most uses the second axis and space will be null, except for concatenating rotations.
        private void RotateMesh(Vector3 axis, Vector3 axis2, float angle, float angle2){
                TempGO.transform.Rotate(axis, angle);  
        }

        // SetActiveMesh() - Ascertain which mesh is currently active, and which mesh should be active and then 
        // switch the mesh ensuring the preservation of the rotation and translation of the current mesh. 
        private void SetActiveMesh(int sel){
            if(Assets[sel].name == CurrentObject.name)
                return;
            else{

                GameObject old = CurrentObject;
                old.SetActive(false);
                CurrentObject = Assets[sel];
                CurrentObject.transform.position = old.transform.position;
                CurrentObject.transform.rotation = old.transform.rotation;
                CurrentObject.SetActive(true);
                if(NewRotationPoint != null){
                    old.transform.parent = null;
                    CurrentObject.transform.parent = NewRotationPoint.transform;
                }
                //if(LitUnlit.isOn)
                //    SetShader(false);
                //else
                //    SetShader(true);
            }

        }
        
        // SetSliceActive(int, int, bool) - Attempt at keeping code down and promote method reuse. Sets the slice on an axis to enabled or disabled. 
        private void SetSliceActive(int axis, int slice, bool enabled){
            switch(axis){
                case 0:
                    if(slice >= current.SliceCount)
                        break;
                    sl_X.slices[slice].SetActive(enabled);
                    break;
                case 1:
                    if(slice >= current.SliceCount)
                        break;
                    sl_Y.slices[slice].SetActive(enabled);
                    break;
                case 2: 
                    if(slice >= current.SliceCount)
                        break;
                    sl_Z.slices[slice].SetActive(enabled);
                    break;
                default: break;
            }

        }
        
        // TranslateMesh() - Translate the mesh relative to the axes which are passed to it from vaarious
        // other functions.
        private void TranslateMesh(string axis){
            bool invertedX, invertedY;
            if(CurrentObject.transform.rotation.y > .5f || CurrentObject.transform.rotation.y < -.5f)
                invertedX = true;
            else
                invertedX = false;

            if(CurrentObject.transform.rotation.x > .5f || CurrentObject.transform.rotation.x < -.5f)
                invertedY = true;
            else
                invertedY = false;

            switch(axis){
                case "x":
                    if(!invertedX)
                        CurrentObject.transform.Translate(Vector3.right * panSpeed * Input.GetAxis("Mouse X") * Time.deltaTime);
                    else
                        CurrentObject.transform.Translate(-Vector3.right * panSpeed * Input.GetAxis("Mouse X") * Time.deltaTime);
                    break;
                case "y":
                    CurrentObject.transform.Translate(Vector3.up * panSpeed * Input.GetAxis("Mouse Y") * Time.deltaTime);
                    break;
                case "xyz":
                    if(!invertedX)
                        CurrentObject.transform.Translate(Vector3.right * panSpeed * Input.GetAxis("Mouse X") * Time.deltaTime);
                    else
                        CurrentObject.transform.Translate(-Vector3.right * panSpeed * Input.GetAxis("Mouse X") * Time.deltaTime);
                    if(!invertedY)
                        CurrentObject.transform.Translate(Vector3.up * panSpeed * Input.GetAxis("Mouse Y") * Time.deltaTime);
                    else
                        CurrentObject.transform.Translate(-Vector3.up * panSpeed * Input.GetAxis("Mouse Y") * Time.deltaTime);
                        //CurrentObject.transform.Translate(Vector3.forward * panSpeed * Input.GetAxis("Mouse X") * Input.GetAxis("Mouse Y") * Time.deltaTime);
                    break;
                case "z":
                    CurrentObject.transform.Translate(Vector3.forward * panSpeed * Input.GetAxis("Mouse Y") * Time.deltaTime);
                    break;
                default:
                    break;
            }
        }
    /// --- USER DEFINED METHODS END

    /// --- UI METHODS BEGIN
        // AutoHideTimer() - uses a timer to countdown a user defined amount before automatically collapsing the menu. 
        IEnumerator AutoHideTimer(){
            yield return new WaitForSeconds(5);
            if(autohidemenu && menuopen && !EventSystem.current.IsPointerOverGameObject()){
                NewMenuActual.transform.Translate (new Vector3(menuOpenPos.x - menuHide, menuOpenPos.y, menuOpenPos.z));
                OpenMenuButton.transform.Rotate(Vector3.forward, 180f);
                menuopen = false;
            }else if( autohidemenu && menuopen && EventSystem.current.IsPointerOverGameObject() ){
                StartCoroutine(AutoHideTimer());
                
            }
            yield break;
        }
        
        // ButtonListener() - Listener for the buttons used in the UI.
        private void ButtonListener(Button target){
            switch(target.name){
                case "OpenMenuButton":
                    if(menuopen){
                        NewMenuActual.transform.Translate (new Vector3(menuOpenPos.x - menuHide, menuOpenPos.y, menuOpenPos.z));
                        OpenMenuButton.transform.Rotate(Vector3.forward, 180f);
                        menuopen = false;

                    }else if(!menuopen){
                        NewMenuActual.transform.Translate (new Vector3(menuOpenPos.x + menuHide, menuOpenPos.y, menuOpenPos.z));
                        OpenMenuButton.transform.Rotate(Vector3.forward, 180f);
                        menuopen = true;
                        if(autohidemenu)
                            StartCoroutine(AutoHideTimer());
                    } 
                    break;
                case "ClearNewRotation":
                        ClearCreatedRotatePoint();
                    break;
                case "Back":      SliceUIButtons(target);   break;
                case "First":     SliceUIButtons(target);   break;
                case "Forward":   SliceUIButtons(target);   break;
                case "Last":      SliceUIButtons(target);   break;
                case "Pause":     SliceUIButtons(target);   break;
                case "Play":      SliceUIButtons(target);   break;
                default:
                    break;
            }
        }
       
        // DropDownListener() - Listener for the drop down menu dialogues in the UI.
        private void DropDownListener(Dropdown target){
            switch(target.name){
                case "RenderWhichMesh":
                        SetActiveMesh(target.value);
                    break;
                case "SelectShaderType":
                        SetShader(target.value);
                    break;
                case "AxesSelect":
                    //first set the currently active axes variables as current to ensure updates (may not be necessary)
                    if ( sl_X.isActive )
                        sl_X = current;
                    else if ( sl_Y.isActive )
                        sl_Y = current;
                    else if ( sl_Z.isActive )
                        sl_Z = current;

                    switch(target.value){
                        case 0: 
                            if ( sl_X.isActive )
                                break;
                            else{
                                sl_X.isActive = true;
                                sl_Y.isActive = false;
                                sl_Z.isActive = false;
                                if(!sl_X.Parent.activeSelf)
                                    sl_X.Parent.SetActive(true);
                                sl_Y.Parent.SetActive(false);
                                sl_Z.Parent.SetActive(false);
                                sl_X.Parent.transform.rotation = CurrentObject.transform.rotation;
                                current = sl_X;
                                SliceUIUpdate();
                            }break;
                        case 1:
                            if ( sl_Y.isActive )
                                break;
                            else{
                                sl_X.isActive = false;
                                sl_Y.isActive = true;
                                sl_Z.isActive = false;
                                if(!sl_Y.Parent.activeSelf)
                                    sl_Y.Parent.SetActive(true);
                                sl_X.Parent.SetActive(false);
                                sl_Z.Parent.SetActive(false);

                                sl_Y.Parent.transform.rotation = CurrentObject.transform.rotation;
                                current = sl_Y;
                                SliceUIUpdate();
                            }break;
                        case 2:
                            if ( sl_Z.isActive )
                                break;
                            else{
                                sl_X.isActive = false;
                                sl_Y.isActive = false;
                                sl_Z.isActive = true;
                                if(!sl_Z.Parent.activeSelf)
                                    sl_Z.Parent.SetActive(true);
                                sl_X.Parent.SetActive(false);
                                sl_Y.Parent.SetActive(false);
                                sl_Z.Parent.transform.rotation = CurrentObject.transform.rotation;
                                current = sl_Z;
                                SliceUIUpdate();
                            }break;

                        default: break;           

                    }//nested switch
                    break;
                default: break;
            }//dropdown switch

        }

        // ErrorShutdown() - Show a dialog box to the user explaining an error has occurred and the application must close
        private void ErrorShutdown(string err){
            // turn off all gameobjects so the scene is empty
            //foreach(GameObject g in FindObjectsOfType<GameObject>() ){
            //    if(g.name != "ScriptHolder")
            //        g.SetActive(false);
            //}
            // create a dialog box showing the error
            string error =  "ERROR \n"; 
            if(Errors.Count != 0){
                foreach (string e in Errors){
                    error += "\n"+e;
                }
            }
            error += " \n \nCould Not Locate Meshes:  Click 'OK' to Exit.";

           GUI.Window(0, new Rect( (Screen.width/2) - 150,(Screen.height/2) - 75, 300, 150), 
                                     ErrorWindowFunction,error);
            
        }

        // ErrorWindowFunction() - Called in the creation of the error dialog box, largely inactive except for creating the ok button.
        private void ErrorWindowFunction(int id){
            if (GUI.Button(new Rect(245, 125, 50, 20), "OK"))
                Application.Quit();
        }

        // OnGUI - Create and display the assetbundle loading progress bar, needed to sanity check this with a check to see if
        // the models were loaded or not as it was creating a rather large memory leak. 03/11/16.
        private void OnGUI(){
            //If we should show the progress bars
            if(showProgress){
                GUI.BeginGroup(new Rect((Screen.width/2) - (pbx/2), (Screen.height/2) - (pby/2), pbx, pby));
                GUI.DrawTexture(new Rect(0, 0, pbx, pby), Empty);
                    GUI.BeginGroup(new Rect(0, 0, pbx * prog, pby));
                    GUI.DrawTexture(new Rect(0, 0, pbx, pby), Fill);
                    GUI.Label(new Rect(10, 5, pbx, pby), "Loading: "+currLoad);
                    GUI.EndGroup();
                GUI.EndGroup();
            } 
            
            if (showBackgroundProgress){
                GUI.BeginGroup(new Rect((Screen.width) - (pbx), (Screen.height) - (pby), pbx, pby));
                GUI.DrawTexture(new Rect(0, 0, pbx, pby), Empty);
                    GUI.BeginGroup(new Rect(0, 0, pbx * bgProg, pby));
                    GUI.DrawTexture(new Rect(0, 0, pbx, pby), Fill);
                    GUI.Label(new Rect(10, 5, pbx, pby), "Loading: " + file);
                    GUI.EndGroup();
                GUI.EndGroup();
            }

            if(showError){
                showProgress = false;
                showBackgroundProgress = false;
                ErrorShutdown("Could not locate meshes, click OK to exit.");
            }
        }
        
        // InputListener() - Listener for the various input fields of the UI.
        private void InputListener(InputField target){
            float ftemp; 
            switch(target.name){
                case "xpos":
                        if(float.TryParse(target.text.ToString(), out ftemp))
                            CurrentObject.transform.position = new Vector3(ftemp,
                                                                     CurrentObject.transform.position.y,
                                                                     CurrentObject.transform.position.z);
                    break;
                case "ypos":
                        if(float.TryParse(target.text.ToString(), out ftemp))
                        CurrentObject.transform.position = new Vector3(CurrentObject.transform.position.x,
                                                                 ftemp,
                                                                 CurrentObject.transform.position.z);
                    break;
                case "zpos":
                        if(float.TryParse(target.text.ToString(), out ftemp))
                        CurrentObject.transform.position = new Vector3(CurrentObject.transform.position.x,
                                                                 CurrentObject.transform.position.y,
                                                                 ftemp);
                    break;
                case "xrot":
                        if(float.TryParse(target.text.ToString(), out ftemp))
                            RotateMesh(-Vector3.up, Vector3.zero, ftemp, 0.0f);
                    break;
                case "yrot":
                        if(float.TryParse(target.text.ToString(), out ftemp))
                            RotateMesh(-Vector3.up, Vector3.zero, ftemp, 0.0f);
                    break;
                case "zrot":
                        if(float.TryParse(target.text.ToString(), out ftemp))
                            RotateMesh(-Vector3.up, Vector3.zero, ftemp, 0.0f);
                    break;
                case "cpx":
                    if(float.TryParse(target.text.ToString(), out ftemp))
                        Cam.transform.position = new Vector3(ftemp, 
                                                             Cam.transform.position.y,
                                                             Cam.transform.position.z);
                    break;
                case "cpy":
                    if(float.TryParse(target.text.ToString(), out ftemp))
                        Cam.transform.position = new Vector3(Cam.transform.position.x, 
                                                             ftemp,
                                                             Cam.transform.position.z);
                    break;
                case "cpz":
                    if(float.TryParse(target.text.ToString(), out ftemp))
                        Cam.transform.position = new Vector3(Cam.transform.position.x, 
                                                             Cam.transform.position.y,
                                                             ftemp);
                    break;
                case "LightBrightness":
                    if(float.TryParse(target.text.ToString(), out ftemp))
                        foreach(Light l in AllLights)
                            l.intensity = ftemp;
                    break;
                case "CurrentSlice": 
                    int t;
                     if( int.TryParse(target.text.ToString(), out t) )
                        if (t > current.SliceCount)
                             t = current.SliceCount;
                        if ( t < 0 ) 
                            t = 0;
                        current.CurrentSlice = t;
                    SliceUIUpdate();
                    break;
                default:
                    break;
            }


        }

        // SetShader(bool) - Apply the unlit or lit shader to the current object being renderered to the screen.
        private void SetShader(int value){
            switch(value){
                case 0://Lit
                        foreach( Renderer r in CurrentObject.GetComponentsInChildren<Renderer>() ){
                           r.material = Lit;
                        }
                    break;
                case 1://Unlit
                        foreach( Renderer r in CurrentObject.GetComponentsInChildren<Renderer>() ){
                           r.material = Unlit;
                        }
                    break;
                case 2://Unshaded
                    foreach( Renderer r in CurrentObject.GetComponentsInChildren<Renderer>() ){
                           r.material = Unshaded;
                        }
                    break;
                default: break;
            }
        }

        // SetupDrawOrientation() - Draw orientation axes to the screen, currently obnoxiously large.
        private void SetupDrawOrientation(){
            float radius = 5f;
            float length = 25f;  
            Vector3 t = new Vector3(radius, length, radius);
            Orientation.transform.rotation = Quaternion.identity;
            Orientation.name = "Orientation Parent";    
            Material unlitcolor = new Material(Shader.Find("Unlit/Color"));
            GameObject ox = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            GameObject oy = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            GameObject oz = GameObject.CreatePrimitive(PrimitiveType.Cylinder);

            ox.transform.SetParent(Orientation.transform, true);
            oy.transform.SetParent(Orientation.transform, true);
            oz.transform.SetParent(Orientation.transform, true);

            ox.transform.localPosition = Vector3.zero;
            oy.transform.localPosition = Vector3.zero;
            oz.transform.localPosition = Vector3.zero;
            ox.transform.localScale = t;
            oy.transform.localScale = t;
            oz.transform.localScale = t;
            ox.transform.Rotate(new Vector3(0.0f, 0.0f, 90.0f));
            oz.transform.Rotate(new Vector3(90.0f, 0.0f, 0.0f));
            
            ox.GetComponent<MeshRenderer>().material = unlitcolor;
            ox.GetComponent<MeshRenderer>().material.SetColor("_Color", Color.red);

            oy.GetComponent<MeshRenderer>().material = unlitcolor;
            oy.GetComponent<MeshRenderer>().material.SetColor("_Color", Color.green);

            oz.GetComponent<MeshRenderer>().material = unlitcolor;
            oz.GetComponent<MeshRenderer>().material.SetColor("_Color", Color.blue);
        }
        
        // SetupUI() - Setup the various UI elements used in the application. 
        private void SetupUI(){
             //--- Reset View UI
            uiText.text     = "Press R to reset View.";

            //---FPS text UI
            fpsText.text = " FPS.";

            {//**New Menu Setup and whatnot
                //Get all of the input fields in the menu 
                InputField[] inputs = NewMenu.GetComponentsInChildren<InputField>();
                //Get all of the Sliders in the menu
                Slider[] sliders = NewMenu.GetComponentsInChildren<Slider>();
                //Get all the buttons 
                Button[] butts = NewMenu.GetComponentsInChildren<Button>();
                RotSens     = sliders[0];
                ZoomSens    = sliders[1];
                {// Setup position and rotation Input Fields
                    ModelXPosIField = null; ModelYPosIField = null; ModelZPosIField = null;
                    ModelXRotIField = null; ModelYRotIField = null; ModelZRotIField = null;
                    foreach(InputField inp in inputs){
                        switch(inp.name){
                            case "xpos": ModelXPosIField = inp; break;
                            case "ypos": ModelYPosIField = inp; break;
                            case "zpos": ModelZPosIField = inp; break;
                            case "xrot": ModelXRotIField = inp; break;
                            case "yrot": ModelYRotIField = inp; break;
                            case "zrot": ModelZRotIField = inp; break;
                            case "LightBrightness": LightBrightnessIField = inp; break;
                            default:               break;                         
                            }
                    }
                    ModelXPosIField.onValueChanged.AddListener(delegate{InputListener(ModelXPosIField);});
                    ModelYPosIField.onValueChanged.AddListener(delegate{InputListener(ModelYPosIField);});
                    ModelZPosIField.onValueChanged.AddListener(delegate{InputListener(ModelZPosIField);});
                    ModelXPosIField.GetComponentInChildren<Text>().text = "0"; 
                    ModelYPosIField.GetComponentInChildren<Text>().text = "0"; 
                    ModelZPosIField.GetComponentInChildren<Text>().text = "0"; 

                    ModelXRotIField.onValueChanged.AddListener(delegate{InputListener(ModelXRotIField);});
                    ModelYRotIField.onValueChanged.AddListener(delegate{InputListener(ModelYRotIField);});
                    ModelZRotIField.onValueChanged.AddListener(delegate{InputListener(ModelZRotIField);});
                    ModelXRotIField.GetComponentInChildren<Text>().text = "0"; 
                    ModelYRotIField.GetComponentInChildren<Text>().text = "0"; 
                    ModelZRotIField.GetComponentInChildren<Text>().text = "0"; 

                    AllLights = FindObjectsOfType<Light>();
                    LightBrightnessIField.onValueChanged.AddListener(delegate{InputListener(LightBrightnessIField);});
                    LightBrightnessIField.text = AllLights[0].intensity.ToString();
                }

                {// setup Camera Position Fields
                    CamXPosIField = null; CamYPosIField = null; CamZPosIField = null;
                    foreach(InputField inp in inputs){
                        switch(inp.name){
                            case "cpx": CamXPosIField = inp; break;
                            case "cpy": CamYPosIField = inp; break;               
                            case "cpz": CamZPosIField = inp; break;
                            default:               break;                         
                            }
                    }
                    CamXPosIField.onValueChanged.AddListener(delegate{InputListener(CamXPosIField);});
                    CamYPosIField.onValueChanged.AddListener(delegate{InputListener(CamYPosIField);});
                    CamZPosIField.onValueChanged.AddListener(delegate{InputListener(CamZPosIField);});
                    CamXPosIField.GetComponentInChildren<Text>().text = "0";
                    CamYPosIField.GetComponentInChildren<Text>().text = "0";
                    CamZPosIField.GetComponentInChildren<Text>().text = "0";
                }

                {// Setup sliders
                    // Rotation sensitivity slider
                    RotSens.onValueChanged.AddListener(delegate{SliderListener(RotSens);});
                    RotSens.minValue        = 0;
                    RotSens.maxValue        = rotSens * 2;
                    RotSens.value           = rotSens;
                    RotSens.wholeNumbers    = true;
                    RotSens.GetComponentInChildren<Text>().text = "Rotation Sensitivity";
                    RotSens.GetComponentInChildren<InputField>().text = rotSens.ToString();

                    // Zoom sensitivity slider
                    ZoomSens.onValueChanged.AddListener(delegate{SliderListener(ZoomSens);});
                    ZoomSens.minValue       = 0;
                    ZoomSens.maxValue       = zoomSens * 2;
                    ZoomSens.value          = zoomSens;
                    ZoomSens.wholeNumbers   = true;
                    ZoomSens.GetComponentInChildren<Text>().text = "Zoom Sensitivity";
                    ZoomSens.GetComponentInChildren<InputField>().text = zoomSens.ToString();
                }
            
                {// Setup dropdown menus
                    MeshSelect.AddOptions(DropDownOptions);
                    MeshSelect.onValueChanged.AddListener(delegate{DropDownListener(MeshSelect);});
                    ShaderSelect.onValueChanged.AddListener(delegate{DropDownListener(ShaderSelect);});
                }

                {// Setup the toggles 
                    orDraw  = true; autohidemenu = false;
                    Toggle[] togs = NewMenu.GetComponentsInChildren<Toggle>();
                    foreach(Toggle tog in togs){
                        switch(tog.name){
                            case "ShowAxisOrientation":
                                OrientationDraw = tog;
                                OrientationDraw.isOn = true;
                                OrientationDraw.onValueChanged.AddListener(delegate{ToggleListener(OrientationDraw);});
                                break;
                            case "Autohide":
                                AutoHideMenu = tog;
                                AutoHideMenu.isOn = true;
                                autohidemenu = true;
                                AutoHideMenu.onValueChanged.AddListener(delegate{ToggleListener(AutoHideMenu);});
                                break;
                            case "ShowSlices":
                                ShowSlices = tog;
                                ShowSlices.isOn = false;
                                ShowSlices.onValueChanged.AddListener(delegate{ToggleListener(ShowSlices);});
                                break;
                            default: break;
                        }
                    }
                }
                
                {// Setup the buttons 
                    foreach(Button b in butts){
                        switch(b.name){
                            case "OpenMenuButton":  OpenMenuButton = b;     break;
                            case "ClearNewRotation":ClearNewRotation = b;   break;
                            default: break;
                        }
                    }
                    //Vector3 menuOpenPos = NewMenu.transform.position;
                    //NewMenu.transform.Translate(new Vector3(menuOpenPos.x - menuHide, menuOpenPos.y, menuOpenPos.z));
                    Vector3 menuOpenPos = NewMenuActual.transform.position;
                    NewMenuActual.transform.Translate(new Vector3(-210, 0, 0));

                    OpenMenuButton.onClick.AddListener(delegate{ButtonListener(OpenMenuButton);});
                    ClearNewRotation.onClick.AddListener(delegate{ButtonListener(ClearNewRotation);});

                    
                }
            } //End of MENU
        }
        
        // ShowMessgae() - Update any string messages used in the UI.
        IEnumerator ShowMessage(string msg, float del){
            uiText.text = msg;
            yield return new WaitForSeconds(del);
            uiText.text  = "Press R to reset View.";
        }

        // SliceUI() - This method will setup the UI for slice interaction as this will be a separate UI entity from the existing UI.
        private void SliceUI(){
            {//buttons begin
                Button[] sliceButts = SliceUIPanel.GetComponentsInChildren<Button>();
                if(sliceButts.Length > 0){
                    foreach(Button b in sliceButts){
                        switch(b.name){
                            case "Back":    slice_Back      = b;    break;
                            case "First":   slice_First     = b;    break;
                            case "Forward": slice_Forward   = b;    break;
                            case "Last":    slice_Last      = b;    break;
                            case "Pause":   slice_Pause     = b;    break;
                            case "Play":    slice_Play      = b;    break;
                            default:                                break;
                        }
                    }
                    slice_Back.onClick.AddListener(delegate{ButtonListener(slice_Back);});
                    slice_First.onClick.AddListener(delegate{ButtonListener(slice_First);});
                    slice_Forward.onClick.AddListener(delegate{ButtonListener(slice_Forward);});
                    slice_Last.onClick.AddListener(delegate{ButtonListener(slice_Last);});
                    slice_Pause.onClick.AddListener(delegate{ButtonListener(slice_Pause);});
                    slice_Play.onClick.AddListener(delegate{ButtonListener(slice_Play);});
                }else{
                    Debug.Log("ERROR: Could not find any sliceUI buttons!");
                }
            }//buttons end

            {//slider begin
                SliceSlider = SliceUIPanel.GetComponentInChildren<Slider>();
                if( SliceSlider != null ){
                    SliceSlider.onValueChanged.AddListener(delegate{SliderListener(SliceSlider);});
                }else{
                    Debug.Log("ERROR: Could not locate sliceSlider!");
                }
            }//slider end

            {//input field begin
                SliceInput = SliceUIPanel.GetComponentInChildren<InputField>();
                if (SliceInput != null ){
                    SliceInput.onValueChanged.AddListener(delegate{InputListener(SliceInput);});
                }else{
                    Debug.Log("ERROR: Could not locate sliceInputField!");
                }
            }//input field end

            {//DROPDOWN BEGIN
                SliceAxisSelector = SliceUIPanel.GetComponentInChildren<Dropdown>();
                if( SliceAxisSelector != null ){
                    SliceAxisSelector.onValueChanged.AddListener(delegate{DropDownListener(SliceAxisSelector);});
                }else{
                    Debug.Log("ERROR: Could not find the Slices Dropdown menu!");
                }
            }//DROPDOWN END

            SliceUIPanel.SetActive(false);
        }

        // SliceUIButtons(Button) - This method is passed a button by the button listener method and then carries out the action for that button.
        private void SliceUIButtons(Button b){
            //Use a switch to handle button inputs 
            switch(b.name){
                case "Back": // step the current slice back by one
                    // check that we are not at the first slice
                    if( current.CurrentSlice > 0 ){
                        // first set the current slice to inactive
                        SetSliceActive( current.AxisID, current.CurrentSlice, false);
                        // step the current slice back
                        current.CurrentSlice--;
                        // set this slice as active
                        SetSliceActive( current.AxisID, current.CurrentSlice, true);
                    }
                break;
                case "First": // set the current slice to the first available slice
                    // check if we are already on the first slice
                    if( current.CurrentSlice == 0 ){
                        break;
                    }else{
                        // first set the current slice to inactive
                        SetSliceActive( current.AxisID, current.CurrentSlice, false);
                        // set current slice to 0
                        current.CurrentSlice = 0;
                        // set this slice as active
                        SetSliceActive( current.AxisID, current.CurrentSlice, true);
                    }
                    break;
                case "Forward": // step the current slice forward by one
                    // check that we are not at the last slice
                    if( current.CurrentSlice < current.SliceCount-1 ){
                        // first set the current slice to inactive
                        SetSliceActive( current.AxisID, current.CurrentSlice, false);
                        // step the current slice forward
                        current.CurrentSlice++;
                        // set this slice as active
                        SetSliceActive( current.AxisID, current.CurrentSlice, true);
                    }
                    break;
                case "Last": // step the current slice to the last available slice
                    // first check that we are not already on the last slice
                    if(current.CurrentSlice == current.SliceCount-1 ){
                        break;
                    }else{
                        // first set the current slice to inactive
                        SetSliceActive( current.AxisID, current.CurrentSlice, false);
                        // set the current slice as last available 
                        current.CurrentSlice = current.SliceCount-1;
                        // set this slice as active
                        SetSliceActive( current.AxisID, current.CurrentSlice, true);
                    }
                    break;
                case "Pause": // if the slices are currently being played then pause the playback
                    // check if playing
                    if(current.isPlaying){
                        current.isPaused = true;
                        current.isPlaying = false;
                        StopCoroutine("SliceUIPlayback");
                        slice_Back.interactable     = true;
                        slice_Forward.interactable  = true;
                        slice_First.interactable    = true;
                        slice_Last.interactable     = true;
                    } else {
                        break;   
                    } break;
                case "Play": // if the slices aren't currently being played, then set to playing. 
                    // Check if paused
                    if(current.isPaused){
                        current.isPaused = false;
                        current.isPlaying = true;
                        StartCoroutine("SliceUIPlayback");
                        slice_Back.interactable     = false;
                        slice_Forward.interactable  = false;
                        slice_First.interactable    = false;
                        slice_Last.interactable     = false;
                    } else {
                        break;   
                    } break;
                default: break;
            }
            SliceUIUpdate();
        }
 
        // SliceUIPlayback() - This Ienumerator method is used to iterate over the total number of slices on any given axis. Currently it waits 
        // around half a second between iterations but this can be easily adjusted by increasing or decreaseing the value of the "delay" variable.
        IEnumerator SliceUIPlayback(){
            float delay = 0.1f;//how long to w
            while(true){
                // if we reach the end then loop around to the beginnning again
                if ( current.CurrentSlice == current.SliceCount-1 ){
                    SetSliceActive( current.AxisID, current.CurrentSlice, false);
                    current.CurrentSlice = 0;
                    SetSliceActive( current.AxisID, current.CurrentSlice, true);
                    SliceUIUpdate(); 
                    yield return new WaitForSeconds(delay);
                } else {
                    // First, set the current slice to inactive
                    SetSliceActive( current.AxisID, current.CurrentSlice, false);
                    // Progress the slice count by one
                    current.CurrentSlice++;
                    // Set the new slice as active
                    SetSliceActive( current.AxisID, current.CurrentSlice, true);
                    // Update the slice UI
                    SliceUIUpdate(); 
                    // Add in the delay so we don't iterate too quickly
                    yield return new WaitForSeconds(delay);
                }
            }
        }

        // SliceUIUpdate() - This method updates the slider UI, typically called after the slider or input field has been used. 
        private void SliceUIUpdate(){
            //SliceSlider.maxValue    = current.SliceCount;
            SliceInput.text         = current.CurrentSlice.ToString();
            SliceSlider.value       = (float)current.CurrentSlice;
            SliceSlider.maxValue    = current.SliceCount-1;
        }
    
        // SliderListener() - Listener for the various sliders of the UI.
        private void SliderListener(Slider target){
            switch(target.name){
                case "RotSens":
                        rotSens = target.value;
                        target.GetComponentInChildren<InputField>().text = rotSens.ToString();
                    break;
                case "ZoomSens":
                        zoomSens = target.value;
                        target.GetComponentInChildren<InputField>().text = zoomSens.ToString();
                    break;
                case "SliceSlider":  
                        SetSliceActive(current.AxisID, current.CurrentSlice, false);
                        current.CurrentSlice = (int)target.value; 
                        SetSliceActive(current.AxisID, current.CurrentSlice, true);
                        SliceUIUpdate();
                    break;
                default:
                    break;
            }
        }

        // ToggleListener() - Listener for the toggle boxes used in the UI.
        private void ToggleListener(Toggle target){
            switch(target.name){
                case "ShowAxisOrientation":
                        if(target.isOn)
                            orDraw = true;
                        else if (!target.isOn)
                            orDraw = false;
                    break;
                case "Autohide":
                    if(target.isOn)
                        autohidemenu = true;
                    else
                        autohidemenu = false;
                    break;
                case "ShowSlices":
                    if(target.isOn){
                        SliceUIPanel.SetActive(true);
                        //CurrentObject.SetActive(false);
                        if(sl_X.isActive){
                            sl_X.Parent.SetActive(true);
                            SetSliceActive(0, current.CurrentSlice, true);
                        }else if (sl_Y.isActive){
                            sl_Y.Parent.SetActive(true);
                            SetSliceActive(1, current.CurrentSlice, true);
                        }else if (sl_Z.isActive){
                            sl_Z.Parent.SetActive(true);
                            SetSliceActive(2, current.CurrentSlice, true);
                        }
                    }else{
                        SliceUIPanel.SetActive(false);
                        CurrentObject.SetActive(true);
                        sl_X.Parent.SetActive(false);
                        sl_Y.Parent.SetActive(false);
                        sl_Z.Parent.SetActive(false);
                    }
                    SliceSlider.maxValue = current.slices.Count-1;
                    break;
                default:
                    break;
            }
        }
        
        // UpdateDrawOrientation() - Apply any updates needed to the orientation axis.
        private void UpdateDrawOrientation(Quaternion rot){
            Orientation.transform.rotation = rot;
        }

        // UpdateUIElements() - Update each of the UI elements that need to be updated at runtime.  
        private void UpdateUIElements(){
            fpsText.text = fps.ToString("F0") +" FPS.";
            ModelXPosIField.GetComponentInChildren<Text>().text = CurrentObject.transform.position.x.ToString();
            ModelYPosIField.GetComponentInChildren<Text>().text = CurrentObject.transform.position.y.ToString();
            ModelZPosIField.GetComponentInChildren<Text>().text = CurrentObject.transform.position.z.ToString();
        
            ModelXRotIField.GetComponentInChildren<Text>().text = CurrentObject.transform.rotation.x.ToString();
            ModelYRotIField.GetComponentInChildren<Text>().text = CurrentObject.transform.rotation.y.ToString();
            ModelZRotIField.GetComponentInChildren<Text>().text = CurrentObject.transform.rotation.z.ToString();

            CamXPosIField.GetComponentInChildren<Text>().text = Cam.transform.position.x.ToString();
            CamYPosIField.GetComponentInChildren<Text>().text = Cam.transform.position.y.ToString();
            CamZPosIField.GetComponentInChildren<Text>().text = Cam.transform.position.z.ToString();

            ZoomSens.value  = zoomSens;
            RotSens.value   = rotSens;

            //Needed calling here due to the asset loading process not blocking until it is complete, sanity check 
            //to ensure the list isn't appeneded to over and over again. 
            if(MeshSelect.options.Count == 0)
                MeshSelect.AddOptions(DropDownOptions);
            
            Rect r = AutoHideMenu.GetComponent<RectTransform>().rect;
            Vector2 test = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            if(menuopen)
                r.position = new Vector2(r.position.x + menuHide, r.position.y);

            if( r.Contains(test)){
                AutoHideMenu.GetComponentInChildren<Text>().text = " Auto Hide Menu";
            }else{
                AutoHideMenu.GetComponentInChildren<Text>().text = "";
            }
            
         
        }
    /// --- UI METHODS END

    // --- ASSET BUNDLE METHODS BEGIN
        // DLABScheduler() - Schedule the DownloadAssetBundleIter() method.
        IEnumerator DLABScheduler(){
            // Import the cp mesh bundle
            file = "cp Mesh";
            yield return StartCoroutine(DownloadAssetBundleIter(assetPath + "default_cp", 0));
            //import the uwnorm mesh bundle
            file = "uw normal Mesh";
            yield return StartCoroutine(DownloadAssetBundleIter(assetPath + "default_uwNorm", 1));
            
            // Load in the slices associated with the mesh from the relevant asset bundles.

            if(!slicesLoaded){
                //Find out how many slices there are and load them into the viewer
                {   /// X-SLICES
                    string abs              = slicePath + "/x";
                    DirectoryInfo di_x      = new DirectoryInfo(abs);
                    FileInfo[] fi_x         = di_x.GetFiles("x_cpt_*");
                    sl_X.SliceCount         = ( fi_x.Length - 4 ) / 4;
                    if( fi_x.Length > 0 ){
                        for(int i = 0; i < sl_X.SliceCount; i++){
                            showProgress = true;
                            string t = preFile + abs + "/x_cpt_" + i;
                            currLoad = "X Slice: "+ i + " of "+ sl_X.SliceCount;
                            file = t;
                            yield return StartCoroutine(SliceBundleDownloader(0, t, false));
                        }
                    } else {
                        Debug.Log("There are no x-slices available for loading.");
                    }
                }
            
                {   /// Y-SLICES
                    string abs              = slicePath + "/y";
                    DirectoryInfo di_y      = new DirectoryInfo(abs);
                    FileInfo[] fi_y         = di_y.GetFiles("y_cpt_*");
                    sl_Y.SliceCount         = ( fi_y.Length - 4 ) / 4;
                    if( fi_y.Length > 0 ){
                        for(int i  = 0; i < sl_Y.SliceCount; i++){
                            showProgress = true;
                            string t = preFile + abs + "/y_cpt_" + i;
                            currLoad = "Y Slice: "+ i + " of "+ sl_Y.SliceCount;
                            file = t;
                            yield return StartCoroutine(SliceBundleDownloader(1, t, false));
                        }
                    } else {
                        Debug.Log("There are no y-slices available for loading.");
                    }
                }

                {   /// Z-SLICES
                    string abs              = slicePath + "/z";
                    DirectoryInfo di_z      = new DirectoryInfo(abs);
                    FileInfo[] fi_z         = di_z.GetFiles("z_cpt_*");
                    sl_Z.SliceCount         = ( fi_z.Length - 4 ) / 4;
                    if( fi_z.Length > 0 ){
                        for(int i  = 0; i < sl_Z.SliceCount; i++){
                            showProgress = true;
                            string t = preFile + abs + "/z_cpt_" + i;
                            currLoad = "Z Slice: "+ i + " of "+ sl_Z.SliceCount;
                            file = t;
                            yield return StartCoroutine(SliceBundleDownloader(2, t, false));
                        }
                    } else {
                        Debug.Log("There are no z-slices available for loading.");
                    }
                }
                slicesLoaded = true;
            }
            // Adjust the slices via the same dimensions as the imported mesh so that they line up. 
            AdjustSlices();

            //Set the active mesh so something renders, sanity check to prevent Null reference exception when no mesh is found
            //Also added some logic to ensure that setting the active mesh for the first time causes no exceptions.
            if(Assets.Count > 0)
                 {CurrentObject = Assets[0]; Assets[0].transform.position = startPos; SetActiveMesh(0); CurrentObject.SetActive(true);}
            else
                showError = true;

            // Added in this final bool just to ensure that the program doesn't attempt to use the associated game objects until they are 
            // fully loaded, as it has a tendency to do so when desaling with asset bundles. 
            FinishedLoading = true;
        }
       
        // BackgroungDownload(int, int) - Attempt to background a number of the slice import processes to reduce the total time it takes for 
        // the program to become interactive; Not working, using coroutines will always block the main thread: needs more research (27/01/17)
        IEnumerator BackgroundDownload(int axis, int countFrom){
            showBackgroundProgress = true;
            showProgress = false;
            string temp = ""; int countTo = 0;
            switch(axis){
                case 0: 
                    countTo = sl_X.SliceCount;
                    temp = preFile + slicePath + "x/x_cpt_";
                    break;
                case 1:
                    countTo = sl_Y.SliceCount;
                    temp = preFile + slicePath + "y/y_cpt_";
                    break;
                case 2: 
                    countTo = sl_Z.SliceCount;
                    temp = preFile + slicePath + "z/z_cpt_";
                    break;
                default: break;
            }
            for(int i = countFrom; i < countTo; i++){
                string t = temp + i; 
                file = t;
                yield return StartCoroutine(SliceBundleDownloader(axis, t, true));
            }

            showBackgroundProgress = false;
        }

        // SliceBundleDownloader(int, string) - This is an enumerator method used in the downloading of asset bundles specific to the slices
        // generated from CFD runs, the axis is passed to the method so the slice can be added to the correct axes slice data structure. 
        IEnumerator SliceBundleDownloader(int axis, string path, bool background){
            GameObject ts = null;
            while(!Caching.ready)
                yield return null;
            //showProgress = true;

            WWW Www = WWW.LoadFromCacheOrDownload(path, 5);
            //Www = new WWW(path);
            yield return Www;
            if(Www.error != null){
                Errors.Add(Www.error);
                yield break;
            }

            AssetBundle slice = Www.assetBundle;
            
            foreach ( string s in slice.GetAllAssetNames() ){
                if( s.IndexOf(".fbx") > -1 ){
                    if(background)
                        ts = Instantiate(slice.LoadAssetAsync(s).asset) as GameObject;
                    else
                        ts = Instantiate(slice.LoadAsset(s)) as GameObject; // generally quicker than async method as async moves thread to background but this method blocks
                    string t = s.Substring( s.IndexOf("/") + 1 );
                    int io = t.IndexOf(".");
                    t.Remove(io);
                    ts.name = t;
                    ts.SetActive(false);
                }
            }

            ts.transform.rotation = Quaternion.identity;
            //So long as the temp object is not null, loop through the list of transform children and ensure that the material 
            //for each is set as the currently used material.
            if(ts != null){
                if(ts.transform.childCount > 0){
                    foreach(Transform t in ts.transform){
                        if(t.childCount > 0){
                            foreach(Transform u in t.transform){
                                u.GetComponent<MeshRenderer>().material = cullOff;
                            }
                        }else{
                            t.GetComponent<MeshRenderer>().material = cullOff;
                        }
                    }
                }else{
                    ts.GetComponent<MeshRenderer>().material = cullOff;
                }
            }

            //Check the axis variable passed to the method and add the asset to the correct data structure
            switch(axis){
                case 0:
                    ts.transform.parent = sl_X.Parent.transform;
                    sl_X.slices.Add(ts); break;
                case 1: 
                    ts.transform.parent = sl_Y.Parent.transform;
                    sl_Y.slices.Add(ts); break;
                case 2:
                    ts.transform.parent = sl_Z.Parent.transform;
                    sl_Z.slices.Add(ts); break;
                default: break;
            }

            slice.Unload(false);
            showProgress = false;
        }

        // DownloadAssetBundlerIter() - Download the asset bundles and set up their relevant parameters, e.g. Material.
        // has been updated to work with the fbx files that we are currently exporting (oct, 2016), where the vertex color
        // data is included in the export from Blender and replaces the texture map.
        IEnumerator DownloadAssetBundleIter(string URL, int type) {
            GameObject dabtemp = null; Material tm = null; Texture2D tt = null; 
            while (!Caching.ready)
                yield return null;
            showProgress = true;
            //if using LoadFromCacheOrDownload it would do just that, load from cache all the time
            //which is obviously not preferable, so using new WWW forces the download. 
            Www = WWW.LoadFromCacheOrDownload(URL, 5);
            //Www = new WWW(URL);
            yield return Www;
            if(Www.error != null){
                //Debug.Log("WWW Error: "+www.error);
                Errors.Add(Www.error);
                yield break;
            }
            AssetBundle assbun =  Www.assetBundle;
            foreach(string str in assbun.GetAllAssetNames()){
                //search the assetbundle for any assets which are model objects, e.g. .obj or .fbx
                if(str.IndexOf(".obj") > -1 || str.IndexOf(".fbx") > -1 ){
                    dabtemp = Instantiate(assbun.LoadAsset(str)) as GameObject;
                    string t = str.Substring(str.IndexOf("_") + 1);
                    int indexof  = t.IndexOf(".");
                    t = t.Remove(indexof);
                    dabtemp.name = t;                    
                    dabtemp.SetActive(false);           
                }
                //If there are any materials load and set them as the material, else create a new
                //material and set its shader as the Vertex Color Lit
                if(str.IndexOf(".mat") > -1)
                    tm = assbun.LoadAsset(str) as Material;
                else{
                    tm = Lit;
                }
                //If there are any textures, load them (deprecated currently)
                if(str.IndexOf(".png") > -1)
                    tt = assbun.LoadAsset(str) as Texture2D;
            }
            //if there are textures for some reason, ensure that the shader is set to the currently used one. 
            if(tt != null){
                tm = Lit;
            }
            //So long as the temp object is not null, loop through the list of transform children and ensure that the material 
            //for each is set as the currently used material.
            if(dabtemp != null){
                foreach(Transform t in dabtemp.transform){
                    if(t.childCount > 0){
                        foreach(Transform u in t.transform){
                            u.GetComponent<MeshRenderer>().material = tm;
                        }
                    }else{
                        t.GetComponent<MeshRenderer>().material = tm;
                    }
                }
            }
            assbun.Unload(false);
            //Add the loaded object to the list of gameobjects representing the meshes
            dabtemp.transform.rotation = startRot;
            CalculateBounds(dabtemp);
            Assets.Add(dabtemp);
            DropDownOptions.Add(dabtemp.name);
            showProgress = false;
        }
    /// --- ASSET BUNDLE METHODS END
}//end of script