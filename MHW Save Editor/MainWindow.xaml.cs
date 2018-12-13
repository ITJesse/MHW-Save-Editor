using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using MHW_Save_Editor.FileFormat;
using MHW_Save_Editor.InvestigationEditing;
using Microsoft.Win32;
using System.Configuration;
using MHW_Save_Editor.Properties;
using MHW_Save_Editor.SaveSlot;

namespace MHW_Save_Editor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private SaveFile saveFile;
        private MemoryStream data;
        
        public bool Slot1Enabled{get=>SlotIsEnabled(0);}
        public bool Slot2Enabled{get=>SlotIsEnabled(1);}
        public bool Slot3Enabled{get=>SlotIsEnabled(2);}

        public bool SlotIsEnabled(int slotindex)
        {
            if (saveFile == null) return false;
            else
            {
                int offset = CharacterSlot.SaveSlotOffset;
                int size = CharacterSlot.SaveSize;
                int current = slotindex;
                int activeoffset = 0xf3888;
                return saveFile.data[offset + size * current + activeoffset] != 0;
            }
        }

        public void RaiseSlotEnabled()
        {
            RaisePropertyChanged("Slot1Enabled");
            RaisePropertyChanged("Slot2Enabled");
            RaisePropertyChanged("Slot3Enabled");
        }
        
        public bool FileLoaded
        {
            get => saveFile != null;
        }

        public MainWindow()
        {           
            data = new MemoryStream();
            InitializeComponent();
            DataContext = this;
        }


        private void OpenFunction(object sender, RoutedEventArgs e)
        {
            string steamPath;
            try{steamPath = Utility.getSteamPath();}
            catch{steamPath = "C:";}
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = steamPath;
            openFileDialog.ShowDialog();

            if (openFileDialog.FileName == "") return;

            try
            {
                saveFile = new SaveFile(File.ReadAllBytes(openFileDialog.FileName));
                RaisePropertyChanged("FileLoaded");
            }
            catch
            {
                MessageBox.Show("Error occurred while attempting to open the file.", "Open Error 1", MessageBoxButton.OK);
                return;
            }
            GeneralTabControl.Size = "Size: " + saveFile.FileSize() + " byte";
            GeneralTabControl.SteamId = "Steam ID: " + saveFile.ReadSteamID();
            GeneralTabControl.Checksum = "Checksum: " + saveFile.GetChecksum();
            GeneralTabControl.OnFileChecksum = "ChecksumGenerated: " + BitConverter.ToString(saveFile.GenerateChecksum()).Replace("-", "");
            GeneralTabControl.FilePath = openFileDialog.FileName;
            GeneralTabControl.Vouchers = "Reset Vouchers";
            InvestigationsTabControl.Content = PopulateInvestigations(saveFile.data);
            InventoryTabControl.Content = PopulateInventory(saveFile.data);
            SlotTabControl.Content = PopulateSlotData(saveFile.data);
            RaiseSlotEnabled();
        }

        private void SaveFunction(object sender, RoutedEventArgs e)
        {
            if (saveFile == null) return;
            var saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Encrypted|*|Unencrypted (*.bin)|*.bin";
            saveFileDialog.InitialDirectory = Utility.getSteamPath();
            saveFileDialog.AddExtension = true;
            Nullable<bool> result = saveFileDialog.ShowDialog();
            if (result == true)
            {
                if (saveFile != null)
                {
                    try
                    {
                        CommitInvestigations();
                        CommitInventory();
                        CommitSlot();
                        saveFile.Save(saveFileDialog.FileName, !(Path.GetExtension(saveFileDialog.FileName) == ".bin"));
                        MessageBox.Show("File saved.", "Save", MessageBoxButton.OK);
                    }
                    catch
                    {
                        MessageBox.Show("Error occurred while attempting to save the file.", "Save Error 1", MessageBoxButton.OK);
                        return;
                    }
                }
                else
                {
                    MessageBox.Show("No File to Save.", "Save", MessageBoxButton.OK);
                }

            }
        }

        private void BackupFunction(object sender, RoutedEventArgs e)
        {
            string steamPath = Utility.getSteamPath();
            string backupPath = Utility.getLocalAppDataPath() + "\\MHW_Save_Editor\\Saves";
            string date_and_time = DateTime.Now.ToString("MM-dd-yyyy_HH-mm-ss");
            Utility.checkBackupDir();

            if (Utility.steamSaveExists())
            {
                try
                {
                    File.Copy(steamPath + "\\SAVEDATA1000", backupPath + "\\SAVEDATA1000_" + date_and_time, true);
                    MessageBox.Show("File backup created in: \n" + backupPath, "Backup", MessageBoxButton.OK);
                }
                catch
                {
                    MessageBox.Show("Error occurred while attempting to copy the file.", "Backup Error 1", MessageBoxButton.OK);
                    return;
                }
            }
        }

        private void RestoreFunction(object sender, RoutedEventArgs e)
        {
            string steamPath = Utility.getSteamPath();
            string backupPath = Utility.getLocalAppDataPath() + "\\MHW_Save_Editor\\Saves";
            Utility.checkBackupDir();

            OpenFileDialog restoreFileDialog = new OpenFileDialog();
            restoreFileDialog.InitialDirectory = backupPath;
            restoreFileDialog.Title = "Restore";
            restoreFileDialog.ShowDialog();

            if (restoreFileDialog.FileName == "") return;

            string backupFile = restoreFileDialog.FileName;

            if (Utility.steamSaveExists())
                if (MessageBox.Show("Are you sure you want to overwrite the existing save with " + backupFile + "?", "Restore", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    try
                    {
                        File.Copy(backupFile, steamPath + "\\SAVEDATA1000", true);
                        MessageBox.Show("File overwrite successful.", "Restore", MessageBoxButton.OK);
                    }
                    catch
                    {
                        MessageBox.Show("Error occurred while attempting to copy the file.", "Restore Error 1", MessageBoxButton.OK);
                        return;
                    }
                }
                else return;
            else
            {
                try
                {
                    File.Copy(backupFile, steamPath + "\\SAVEDATA1000", true);
                    MessageBox.Show("File restore successful.", "Restore", MessageBoxButton.OK);
                }
                catch
                {
                    MessageBox.Show("Error occurred while attempting to copy the file.", "Restore Error 2", MessageBoxButton.OK);
                    return;
                }
            }
        }

        private void SetSlot0Active(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.activeSlot = 0;
            checkSlotMenu(sender, e);
            rePopulate(sender, e);
        }

        private void SetSlot1Active(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.activeSlot = 1;
            checkSlotMenu(sender, e);
            rePopulate(sender, e);
        }

        private void SetSlot2Active(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.activeSlot = 2;
            checkSlotMenu(sender, e);
            rePopulate(sender, e);
        }

        private void checkSlotMenu(object sender, RoutedEventArgs e)
        {
            if (Properties.Settings.Default.activeSlot == 0) { Slot0.IsChecked = true; Slot1.IsChecked = false; Slot2.IsChecked = false; }
            if (Properties.Settings.Default.activeSlot == 1) { Slot0.IsChecked = false; Slot1.IsChecked = true; Slot2.IsChecked = false; }
            if (Properties.Settings.Default.activeSlot == 2) { Slot0.IsChecked = false; Slot1.IsChecked = false; Slot2.IsChecked = true; }
        }

        private void clearMem(object sender, RoutedEventArgs e)
        {
            GC.Collect();
        }

        private void rePopulate(object sender, RoutedEventArgs e)
        {
            InvestigationsTabControl.Content = PopulateInvestigations(saveFile.data);
            InventoryTabControl.Content = PopulateInventory(saveFile.data);
            SlotTabControl.Content = PopulateSlotData(saveFile.data);
            GC.Collect();
        }

        void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl)
            {
                RaisePropertyChanged("EditOptions");
                RaisePropertyChanged("ToolsOptions");
            }
        }

        public ObservableCollection<string> EditOptions
        {
            get
            {
                switch(TabControl.SelectedIndex)
                {
                    case 2:
                        return new ObservableCollection<string>(InvestigationList_EditMenu);
                    default:
                        return new ObservableCollection<string>();
                }
            }
        }

        public ObservableCollection<string> ToolsOptions
        {
            get
            {
                switch (TabControl.SelectedIndex)
                {
                    case 2:
                        return new ObservableCollection<string>(InvestigationList_ToolsMenu);
                    default:
                        return new ObservableCollection<string>();
                }
            }
        }

        private void GeneralEditHandler(string any)
        {}
        private void GeneralToolsHandler(string any)
        {}
        
        private void EditHandlers(int switchvar, string command)
        {
            switch (switchvar)
            {
                case (0):
                    GeneralEditHandler(command);
                    break;
                case (2):
                    InvestigationsEditHandler(command);
                    break;
            }
        }

        private void ToolsHandlers(int switchvar, string command)
        {
            switch (switchvar)
            {
                case (0):
                    GeneralToolsHandler(command);
                    break;
                case (2):
                    InvestigationsToolsHandler(command);
                    break;
            }
        }

        private void EditMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MenuItem  obMenuItem = e.OriginalSource as MenuItem ;
            EditHandlers(TabControl.SelectedIndex,obMenuItem.Header.ToString());
        }

        private void ToolsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MenuItem  obMenuItem = e.OriginalSource as MenuItem ;
            ToolsHandlers(TabControl.SelectedIndex,obMenuItem.Header.ToString());
        }
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        private void RaisePropertyChanged(string propertyName)
        {
            // take a copy to prevent thread issues
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
    
}
