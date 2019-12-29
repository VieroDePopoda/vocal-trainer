using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.Midi;
using System.Windows;

namespace VocalTrainer
{
    internal class AudioSintezator: IDisposable
    {

        private readonly MidiOut _midiOut = new MidiOut(0);
        private readonly List<int> _playingTones = new List<int>();
        private int Chanel = 1;
        public void Dispose()
        {
            _midiOut.Close();
            _midiOut.Dispose();
        }

        public int PlayTone(byte octave, Tones tone, int strength = 127) //Воспроизводим желаемую ноту (напр. "C#5").
        {
            int note = 12 + octave * 12 + (int)tone; // 12 полутонов в октаве, начинаем считать с 1-й октавы

            if (!_playingTones.Contains(note))
            {
                _midiOut.Send(MidiMessage.StartNote(note, strength, Chanel).RawData); // воспроизводим ноту на основном канале
                _playingTones.Add(note);
            }

            return note;
        }

        public void PlayTone(string str, int strength = 127) //Берем строку и конвертируем в ноту
        {
            Note parseNote = Note.FromString(str);
            int note = 12 + parseNote.Octave * 12 + (int)parseNote.Tone;

            _midiOut.Send(MidiMessage.StartNote(note, strength, Chanel).RawData);

        }

        public void StopPlaying(int id)
        {
            if (_playingTones.Contains(id))
            {
                _midiOut.Send(MidiMessage.StopNote(id, 0, Chanel).RawData);
                _playingTones.Remove(id);
            }
        }

        public void StopPlaying(byte octave, Tones tone)
        {
            StopPlaying(12 + octave * 12 + (int)tone);
        }

        public void StopAll()
        {
            while (_playingTones.Count > 0)
            {
                StopPlaying(_playingTones.First());
            }

            _playingTones.Clear();
        }
    }

    internal struct Note
    {
        public int Id;
        public byte Octave;
        public Tones Tone;

        public Note(byte oct, Tones t)
        {
            Octave = oct;
            Tone = t;

            Id = 12 + Octave * 12 + (int)Tone;
        }

        public static Note FromString(string str)
        {
            byte octave = byte.Parse(str.Last().ToString());
            var tone = str.Substring(0, str.Length - 1).Replace('#', 'd').ConvertToEnum<Tones>();

            return new Note(octave, tone);
        }

        public static Note FromID(int id)
        {
            return new Note((byte)(id / 12 - 1), (Tones)(id % 12));
        }

        public bool isDiez()
        {
            return Tone.ToString().Contains('d');
        }

        #region Operators Defenition

        public static int operator -(Note note1, Note note2)
        {
            return Math.Abs(note1.Id - note2.Id);
        }

        public static Note operator +(Note note, int semitons)
        {
            var octave = (byte)(note.Octave + semitons / 12); // 12 полутонов в октаве
            int tmp = (int)note.Tone + semitons % 12;
            if (tmp > (int)Tones.B) // Последняя нота в октаве
            {
                ++octave;
                tmp = tmp % 12;
            }
            var tone = (Tones)(tmp);

            return new Note(octave, tone);
        }

        public static bool operator <(Note note1, Note note2)
        {
            return note1.Id < note2.Id;
        }

        public static bool operator >(Note note1, Note note2)
        {
            return note1.Id > note2.Id;
        }

        public static bool operator >=(Note note1, Note note2)
        {
            return note1.Id >= note2.Id;
        }

        public static bool operator <=(Note note1, Note note2)
        {
            return note1.Id <= note2.Id;
        }

        public static bool operator ==(Note note1, Note note2)
        {
            return note1.Id == note2.Id;
        }

        public static bool operator !=(Note note1, Note note2)
        {
            return note1.Id != note2.Id;
        }

        public override string ToString()
        {
            return Tone.ToString().Replace('d', '#') + Octave;
        }

        #endregion
    }

    public enum Tones
    {
        A = 9,
        Ad = 10,
        B = 11,
        C = 0,
        Cd = 1,
        D = 2,
        Dd = 3,
        E = 4,
        F = 5,
        Fd = 6,
        G = 7,
        Gd = 8
    }
}
