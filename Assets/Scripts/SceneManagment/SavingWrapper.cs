using System;
using System.Collections;
using System.Collections.Generic;
using RPG.Saving;
using UnityEngine;

namespace RPG.SceneManagement
{
    public class SavingWrapper : MonoBehaviour
    {
        const string defaultSaveFile = "save";

        private SavingSystem savingSystem;

        private void Start()
        {
            savingSystem = GetComponent<SavingSystem>();
        }

        // Update is called once per frame
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.L))
            {
                Load();
            }

            if (Input.GetKeyDown(KeyCode.S))
            {
                Save();
            }
        }

        private void Save()
        {
            savingSystem.Save(defaultSaveFile);
        }

        private void Load()
        {
            savingSystem.Load(defaultSaveFile);
        }
    }
}