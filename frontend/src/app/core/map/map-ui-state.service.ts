import { Injectable, computed, signal } from '@angular/core';

export type SheetState = 'collapsed' | 'half' | 'expanded';

/**
 * Coordinates the map with the floating UI above it — the Angular-native
 * translation of the layer/padding/camera separation used in mobility-app map
 * architectures: panels and sheets never manipulate the map directly, they
 * publish UI state here and the map component derives padding/selection from it.
 * Provided per screen (component-level provider), not as a root singleton.
 */
@Injectable()
export class MapUiStateService {
  /** Current bottom-sheet position on this screen. */
  readonly sheetState = signal<SheetState>('collapsed');

  /** Stop currently selected via marker or list (null = none). */
  readonly selectedStopId = signal<string | null>(null);

  /** Map bottom padding in px so fitBounds keeps content above the sheet. */
  readonly bottomPadding = computed(() => {
    switch (this.sheetState()) {
      case 'expanded': return 320; // sheet covers most — keep markers in upper part
      case 'half': return 220;
      default: return 120;         // collapsed peek
    }
  });

  setSheetState(state: SheetState) { this.sheetState.set(state); }
  selectStop(id: string | null) { this.selectedStopId.set(id); }
}
