class SettingsFieldFileUpload {

    public showFilePicker(inputFile: HTMLInputElement) {
        inputFile.value = ''; // Clear selected file to ensure onchange is triggered at all times

        inputFile.showPicker();
    }
}

export function init() {
    return new SettingsFieldFileUpload();
}
