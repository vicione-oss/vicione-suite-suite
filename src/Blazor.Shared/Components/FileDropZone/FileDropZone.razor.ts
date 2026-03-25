import '/_content/ViciOne.Suite.Blazor.Shared/js/event-target-mixins.js';

export class FileDropZone {
    private inputFile: HTMLInputElement | undefined = undefined;

    private dragStartEventListenerBinding: ((e: DragEvent) => void) | undefined = undefined;
    private dragEnterEventListenerBinding: ((e: DragEvent) => void) | undefined = undefined;
    private dragOverEventListenerBinding: ((e: DragEvent) => void) | undefined = undefined;
    private dragLeaveEventListenerBinding: ((e: DragEvent) => void) | undefined = undefined;
    private dropEventListenerBinding: ((e: DragEvent) => void) | undefined = undefined;
    constructor(readonly dropZone: HTMLElement, readonly dotNetObject: DotNet.DotNetObject, inputFile: HTMLInputElement | undefined) {
        this.inputFile = inputFile;

        this.dragStartEventListenerBinding = this.dragStart.bind(this);
        this.dropZone.addEventListener('dragstart', this.dragStartEventListenerBinding);

        this.dragEnterEventListenerBinding = this.dragEnter.bind(this);
        this.dropZone.addEventListener('dragenter', this.dragEnterEventListenerBinding);

        this.dragOverEventListenerBinding = this.dragOver.bind(this);
        this.dropZone.addEventListener('dragover', this.dragOverEventListenerBinding);

        this.dragLeaveEventListenerBinding = this.dragLeave.bind(this);
        this.dropZone.addEventListener('dragleave', this.dragLeaveEventListenerBinding);

        this.dropEventListenerBinding = this.drop.bind(this);
        this.dropZone.addEventListener('drop', this.dropEventListenerBinding);
    }

    public setInputFile(inputFile: HTMLInputElement | undefined) {
        this.inputFile = inputFile;
    }

    public dispose() {
        if (this.dropEventListenerBinding)
            this.dropZone.removeEventListener('drop', this.dropEventListenerBinding);

        if (this.dragStartEventListenerBinding)
            this.dropZone.removeEventListener('dragstart', this.dragStartEventListenerBinding);

        if (this.dragLeaveEventListenerBinding)
            this.dropZone.removeEventListener('dragleave', this.dragLeaveEventListenerBinding);

        if (this.dragOverEventListenerBinding)
            this.dropZone.removeEventListener('dragover', this.dragOverEventListenerBinding);

        if (this.dragEnterEventListenerBinding)
            this.dropZone.removeEventListener('dragenter', this.dragEnterEventListenerBinding);
    }

    private async dragStart(e: DragEvent) {
        // The event "dragstart" is not fired for file drags from the OS, see https://developer.mozilla.org/en-US/docs/Web/API/HTML_Drag_and_Drop_API/File_drag_and_drop.
        // Hence we simply cancel the event and therefore disallow a drop of the currently dragged item(s)
        e.preventDefault();
    }

    private async dragEnter(e: DragEvent) {
        await this.handleDragEnter(e);
    }

    private async dragOver(e: DragEvent) {
        // https://developer.mozilla.org/en-US/docs/Web/API/HTML_Drag_and_Drop_API/File_drag_and_drop#prevent_the_browsers_default_drag_behavior
        e.preventDefault();
    }

    private isSingleFileDrag(dataTransfer: DataTransfer | undefined) {
        if (dataTransfer?.items.length === 1) {
            const dataTransferItem = dataTransfer.items[0];

            return dataTransferItem.kind === 'file';
        }

        return false;
    }

    private async handleDragEnter(e: DragEvent) {
        if (e.currentTarget !== this.dropZone)
            return;

        if (!e.dataTransfer)
            return;

        const continue_ = this.isSingleFileDrag(e.dataTransfer);

        if (continue_) {
            // https://developer.mozilla.org/en-US/docs/Web/API/HTML_Drag_and_Drop_API/File_drag_and_drop#prevent_the_browsers_default_drag_behavior
            e.preventDefault();

            e.dataTransfer.dropEffect = 'copy';

            await this.dotNetObject.invokeMethodAsync('DragEnter');

        } else {
            e.dataTransfer.dropEffect = 'none';
        }
    }

    private async dragLeave(e: DragEvent) {
        const from = e.target;
        const to = e.relatedTarget;

        if (from?.isNestedHtmlElementOf(this.dropZone)) {
            if (to === this.dropZone)
                return;

            if (to && !to.isNestedHtmlElementOf(this.dropZone)) {
                await this.executeDragLeave();

                return;
            }
        }

        if (e.currentTarget !== this.dropZone)
            return;

        if (to?.isNestedHtmlElementOf(this.dropZone))
            return;

        if (from?.isNestedHtmlElementOf(this.dropZone))
            return;

        await this.executeDragLeave();
    }

    private async executeDragLeave() {
        await this.dotNetObject.invokeMethodAsync('DragLeave');
    }

    private async drop(e: DragEvent) {
        e.preventDefault();

        if (this.inputFile) {
            this.inputFile.files = e.dataTransfer!.files;
            this.inputFile.dispatchEvent(new Event('change', { bubbles: true }));
        }

        await this.dotNetObject.invokeMethodAsync('Drop');
    }
}

