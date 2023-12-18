using Unity.VisualScripting;
using UnityEngine;

namespace EMPACResearch.Core.Audio
{ 
    public class VirtualSoundSourceTracker : MonoBehaviour
    {
        /// <summary>
        /// VirtualSoundSourceTracker : MonoBehaviour
        /// ===================================================================
        /// Configure virtual sound sources for spatial audio display at ROIS.
        /// -------------------------------------------------------------------
        /// Inputs:
        /// 1) bool autoDetect
        ///    Automatic detection of virtual sound sources in the scene.
        /// 2) OSC osc
        ///    Network configuration for Open Sound Control (OSC).
        /// 3) GameObject controller
        ///    The controller for ROIS in the scene.
        /// 4) GameObject[] virtualSoundSources
        ///    The designated virtual sound sources in the scene.
        /// ===================================================================
        /// [c] 2023 EMPAC Research
        /// </summary>

        [SerializeField]
        bool autoDetect;

        [SerializeField] 
        OSC osc;

        [SerializeField]
        GameObject controller;

        [SerializeField]
        GameObject[] virtualSoundSources;

        [Header("Advanced Features")]

        [SerializeField]
        bool useObjectTrigger;

        [SerializeField]
        bool useDistancedGain;

        [SerializeField]
        bool useDopplerEffect;

        [SerializeField, Range(90f, 180f)]
        float ambientAperture = 180f;

        [SerializeField, Range(-72f, 0f)]
        float backgroundLevel = -30f;


        /// <summary>
        /// Private variables:
        /// 1) int virtualSoundSourceCount
        ///    Number of virtual sound source positions;
        /// 2) Vector3[] relativeSourcePositions
        ///    The collection of sound source positions relative to ROIS's 
        ///    position and heading in real time;
        /// 3) Vector3 controllerPosition
        ///    The position of ROIS controller in the scene in real time;
        /// 4) float controllerHeading
        ///    Y-rotation of ROIS controller in the scene in real time.
        /// </summary>
        int virtualSoundSourceCount;
        Vector3[] relativeSourcePositions;
        float[] relativeDistances;

        Vector3 controllerPosition;
        float controllerHeading;


        private void OnEnable()
        {
            /* Detect virtual sound sources in the environment if none is 
             * manually assigned. */
            if (autoDetect) virtualSoundSources = 
                GameObject.FindGameObjectsWithTag("SoundSource");

        }


        private void Start()
        {
            /* Communicate the initialization of audio playback via OSC to the 
             * audio workstation. */
            SendPlaybackStatus(1);
            virtualSoundSourceCount = virtualSoundSources.Length;
            relativeDistances = new float[virtualSoundSourceCount];

            for (int i = 0; i < virtualSoundSourceCount; i++)
            {
                if (useDopplerEffect)
                    SendDopplerEffectStatus(i, 1);

                if (virtualSoundSources[i].layer == 10)
                {
                    SendAmbientAperture(i, ambientAperture);
                    SendBackgroundLevel(i, backgroundLevel);
                }
            }
        }


        private void Update()
        {
            /* Get controller position and heading in real time. */
            controllerPosition = controller.transform.position;
            controllerHeading = controller.transform.rotation.eulerAngles.y; 

            /* Compute relative positions of virtual sound sources */
            relativeSourcePositions = 
                GetRelativeSourcePositions(virtualSoundSources);

            /* Send updated virtual sound source information via OSC */
            for (int i = 0; i < virtualSoundSourceCount; i++)
            {
                SendSourcePosition(i, relativeSourcePositions[i]);
                // if (useObjectTrigger) SendObjectTriggerStatus(i, 1); // to be implemented
                if (useDistancedGain)
                {
                    relativeDistances[i] = Vector3.Distance(virtualSoundSources[i].transform.position, controllerPosition);
                    
                }
            }
            if (useDistancedGain) SendDistancedGain(relativeDistances);

        }


        private void OnApplicationQuit()
        {
            /* Then, communicate the termination of audio playback via OSC to 
             * the audio workstation. */
            SendPlaybackStatus(0);
        }


        private void OnDisable()
        {
            /* When the session ends, first clear the virtual sound sources
             * to avoid memory leak. */
            if (virtualSoundSources != null) virtualSoundSources = null;
        }


        private void OnDestroy()
        {
            SendPlaybackStatus(0);
        }


        /// <summary>
        /// Vector3[] GetRelativeSourcePositions(GameObject[] virtualSoundSources)
        /// ----------------------------------------------------------------------
        /// Compute the relative positions of virtual sound sources in the scene
        /// based upon the position and headings of the controller.
        /// </summary>
        /// <param name="virtualSoundSources">
        /// The virtual sound sources to compute relative positions to the controller.
        /// </param>
        /// <returns>A vector of relative sound source positions</returns>
        Vector3[] GetRelativeSourcePositions(GameObject[] virtualSoundSources)
        {
            Vector3[] relativePositions = new Vector3[virtualSoundSourceCount];
            Vector3 referencePosition;
            float referenceAngle, distance, relativeAngle;

            for (int i = 0; i < virtualSoundSourceCount; i++)
            {
                /* Calculate a reference position between the controller and the sound source
                 * without taking into account the controller's heading. */
                referencePosition = new Vector3(
                    virtualSoundSources[i].transform.position.x - controllerPosition.x, 0f,
                    virtualSoundSources[i].transform.position.z - controllerPosition.z);

                /* Determine the absolute angle of the virtual sound source based upon the 
                 * positive x-axis of the world coordinates (right-hand rule). */
                referenceAngle = Vector3.SignedAngle(
                    referencePosition, Vector3.right, Vector3.up
                );
                
                /* Calculate the distance between the sound source and the controller. */
                distance = Vector3.Distance(
                    virtualSoundSources[i].transform.position, controllerPosition
                );

                /* Calculate the relative angle between the controller's heading 
                 * and the direction of the virtual sound source. */ 
                relativeAngle = Mathf.Deg2Rad * (controllerHeading + referenceAngle);

                /* Finally, calculate the relative positions based upon the 
                 * relative angle. */
                relativePositions[i] = new Vector3(
                    distance * Mathf.Cos(relativeAngle), 0f,
                    distance * Mathf.Sin(relativeAngle)
                    );
            }

            return relativePositions;
        }


        /// <summary>
        /// void SendPlaybackStatus(int status)
        /// -------------------------------------------------------------------
        /// Communicate to the audio workstation to turn the audio materials 
        /// on and off.
        /// </summary>
        /// <param name="status">The status of play back: on (1) and off (0).</param>
        void SendPlaybackStatus(int status)
        {
            OscMessage msg = new OscMessage();
            msg.address = "/status";
            msg.values.Add(status);
            osc.Send(msg);
        }


        void SendObjectTriggerStatus(int id, int status)
        {
            OscMessage msg = new OscMessage();
            msg.address = "/source" + (id + 1).ToString() + "/status";
            msg.values.Add(status);
            osc.Send(msg);
            Debug.Log(msg);
        }


        /// <summary>
        /// void SendSourcePositions(Vector3[] positions)
        /// -------------------------------------------------------------------
        /// Communicate to the audio workstation the relative positions of all 
        /// virtual sound sources.
        /// </summary>
        /// <param name="positions">Positions of virtual sound sources</param>
        void SendSourcePosition(int id, Vector3 position)
        {
            OscMessage msg = new OscMessage();
            msg.address = "/source/" + (id + 1).ToString() + "/xy";
            msg.values.Add(position.x);    
            msg.values.Add(position.z);
            osc.Send(msg);
            Debug.Log(msg);
        }


        void SendDistancedGain(float[] distances, float rollOff = -1f)
        {
            /* Assumes linear decay */
            OscMessage msg = new OscMessage();
            msg.address = "/distances";
            for (int i = 0; i < distances.Length; i++) 
            {
                float gain = (distances[i] * rollOff < -70f) ?
                    -70f : distances[i] * rollOff;
                msg.values.Add(gain);
            }
            osc.Send(msg);
            Debug.Log(msg);
        }


        void SendDopplerEffectStatus(int id, int status)
        {
            OscMessage msg = new OscMessage();
            msg.address = "/source/" + (id + 1).ToString() + "/doppler";
            msg.values.Add(status);
            osc.Send(msg);
            Debug.Log(msg);
        }


        void SendAmbientAperture(int id, float aperture)
        {
            OscMessage msg = new OscMessage();
            msg.address = "/source/" + (id + 1).ToString() + "/aperture";
            msg.values.Add(aperture);
            osc.Send(msg);
            Debug.Log(msg);
        }


        void SendBackgroundLevel(int id, float level)
        {
            OscMessage msg = new OscMessage();
            msg.address = "/src/" + (id + 1).ToString() + "/gain";
            msg.values.Add(level);
            osc.Send(msg);
            Debug.Log(msg);
        }

    }
}
