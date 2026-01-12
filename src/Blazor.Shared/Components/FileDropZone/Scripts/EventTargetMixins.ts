// https://www.typescriptlang.org/docs/handbook/mixins.html#alternative-pattern

class EventTargetMixins {
    /**
     * @param rootAncestor - Element that is the potential root ancestor of "this"
     */
    isNestedHtmlElementOf(this: EventTarget, rootAncestor: HTMLElement): boolean {
        if (this instanceof HTMLElement) {

            // Traverse through the ancestors ...
            let ancestor = this.parentElement;
            while (ancestor
                && ancestor !== rootAncestor) { // ... as long as the we don't find our root ancestor ...

                ancestor = ancestor.parentElement;
            }

            // If we found our root anchestor ...
            if (ancestor)
                return true; // ... then "this" is nested in root anchestor
        }

        return false;
    }
}

// eslint-disable-next-line @typescript-eslint/consistent-type-definitions
interface EventTarget extends EventTargetMixins { }

applyMixins(EventTarget, [EventTargetMixins]);

function applyMixins(derivedCtor: any, constructors: any[]) {
    constructors.forEach(baseCtor => {
        Object.getOwnPropertyNames(baseCtor.prototype).forEach(name => {
            Object.defineProperty(
                derivedCtor.prototype,
                name,
                // eslint-disable-next-line @typescript-eslint/no-unsafe-argument
                Object.getOwnPropertyDescriptor(baseCtor.prototype, name) ?? Object.create(null)
            );
        });
    });
}
