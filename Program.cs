using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;

T GetDevice<T>(List<T> devices, string itemName)
{
    do
    {
        Console.WriteLine($"Select {itemName}:");
        var index = 1;

#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
        foreach (dynamic device in devices)
        {
            Console.WriteLine($"\t{index++}: {device!.Name}");
        }
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.

        var input = Console.ReadLine();
        if (int.TryParse(input, out index))
        {
            if (index <= 0 || (index - 1) >= devices.Count)
            {
                Console.WriteLine($"No device found at index {index}");
            }
            else
            {
                return devices[index - 1];
            }
        }
        else
        {
            Console.WriteLine($"Invalid input - please type the index of the device");
        }
    }
    while (true);
}

var inputDevice = GetDevice(InputDevice.GetAll().ToList(), "input device");
var outputDevice = GetDevice(OutputDevice.GetAll().ToList(), "output device");
var voices = GetDevice(Enumerable.Range(1, 8).Select(i => new { Voices = i, Name = $"{i}" }).ToList(), "how many voices").Voices;

Console.WriteLine();
Console.WriteLine($"Reading MIDI from all channels of {inputDevice.Name}");
Console.WriteLine($"Dispatching MIDI to {outputDevice.Name} on MIDI Channel 1 to {voices}");
Console.WriteLine("Press any key to exit");
Console.WriteLine();

var activeVoices = new Dictionary<SevenBitNumber, FourBitNumber>();
var reversedActiveVoices = new Dictionary<FourBitNumber, SevenBitNumber>();
var currentVoice = -1;

var resetLeft = Console.CursorLeft;
var resetTop = Console.CursorTop;

var notes = new [] { "A", "A#", "B", "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#" };
void PrintVoices()
{
    Console.SetCursorPosition(resetLeft, resetTop);
    foreach(var voice in Enumerable.Range(1, voices))
    {
        Console.Write(string.Format("|{0,3}  ", voice));
    }
    Console.Write("|");

    Console.WriteLine();

    foreach (var voice in Enumerable.Range(0, voices))
    {
        string note = "";
        if (reversedActiveVoices.TryGetValue(new FourBitNumber((byte)voice), out var played))
        {
            var playedInt = ((int)played);
            if(playedInt < 21)
            {
                note = "ERR";
                continue;
            }
            var octave = playedInt / 12 + 1;
            note = notes[(playedInt - 21) % 12] + octave;
        }
        Console.Write(string.Format("|{0,4} ", note));
    }
    Console.Write("|");
}

PrintVoices();
inputDevice.EventReceived += (o, e) =>
{
    var changed = false;
    switch(e.Event.EventType)
    {
        case MidiEventType.NoteOn:
            currentVoice = (currentVoice + 1) % voices;
            var noteOn = (NoteOnEvent)e.Event.Clone();
            noteOn.Channel = new FourBitNumber((byte)currentVoice);
            outputDevice.SendEvent(noteOn);

            activeVoices[noteOn.NoteNumber] = noteOn.Channel;
            reversedActiveVoices[noteOn.Channel] = noteOn.NoteNumber;
            changed = true;
            break;
        case MidiEventType.NoteOff:

            var noteOff = (NoteOffEvent)e.Event.Clone();
            if(activeVoices.TryGetValue(noteOff.NoteNumber, out var channel))
            {
                noteOff.Channel = channel;
                outputDevice.SendEvent(noteOff);
                changed = true;
                activeVoices.Remove(noteOff.NoteNumber);
                reversedActiveVoices.Remove(channel);
            }
            break;
        default:
            outputDevice.SendEvent(e.Event);
            break;
    }

    if(changed)
    {
        PrintVoices();
    }
};

inputDevice.StartEventsListening();

Console.ReadKey();

inputDevice.Dispose();

Console.WriteLine("Exit");