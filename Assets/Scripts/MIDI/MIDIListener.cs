using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class MIDIListener : MonoBehaviour
{

    protected float currentSensorOffset = 0;

    protected SlimeSettings settings = null;

    // Start is called before the first frame update
    void Start()
    {
        InputSystem.onDeviceChange += (device, change) => 
        {
            var simulation = GameObject.FindObjectOfType<Simulation>();
            if (simulation == null) return;
            settings = simulation.settings;

            if (change != InputDeviceChange.Added) return;

            var midiDevice = device as Minis.MidiDevice;
            if (midiDevice == null) return;

            // Setup listeners
            midiDevice.onWillControlChange += (control, value) => {
                Debug.Log(string.Format(
                    "Control Change #{0} ({1}) value:{2:0.00} ch:{3} dev:'{4}'",
                    control.controlNumber,
                    control.shortDisplayName,
                    value,
                    (control.device as Minis.MidiDevice)?.channel,
                    control.device.description.product
                ));

                HandleControlChanged(control, value);
            };

            midiDevice.onWillNoteOn += (note, velocity) => {
                Debug.Log(string.Format(
                    "Note On #{0} ({1}) vel:{2:0.00} ch:{3} dev:'{4}'",
                    note.noteNumber,
                    note.shortDisplayName,
                    velocity,
                    (note.device as Minis.MidiDevice)?.channel,
                    note.device.description.product
                ));

                HandleNoteOn(note, velocity);
            };

            midiDevice.onWillNoteOff += (note) => {
                Debug.Log(string.Format(
                    "Note Off #{0} ({1}) ch:{2} dev:'{3}'",
                    note.noteNumber,
                    note.shortDisplayName,
                    (note.device as Minis.MidiDevice)?.channel,
                    note.device.description.product
                ));
                
                HandleNoteOff(note);
            };
        };
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    /// Called when a MIDI note is pressed
    private void HandleNoteOn(Minis.MidiNoteControl note, float velocity) {
        switch (note.noteNumber) {
            case 53:
                currentSensorOffset = settings.speciesSettings[0].sensorOffsetDst;
                settings.speciesSettings[0].sensorOffsetDst = 0;
                break;
        }
    }

    /// Called when a MIDI note is released
    private void HandleNoteOff(Minis.MidiNoteControl note) {
        switch (note.noteNumber) {
            case 53:
                settings.speciesSettings[0].sensorOffsetDst = currentSensorOffset;
                break;
        }
    }

    /// Called when a MIDI CC value has changed
    private void HandleControlChanged(Minis.MidiValueControl control, float value) {
        switch (control.controlNumber) {
            case 1:
                settings.trailWeight = value * 10;
                break;

            case 2:
                settings.decayRate = value;
                break;

            case 3:
                settings.speciesSettings[0].moveSpeed = value * 100;
                break;

            case 4:
                settings.speciesSettings[0].sensorOffsetDst = value * 100;
                break;
        }
    }
}
