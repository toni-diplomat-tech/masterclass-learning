import { useEffect, useState, useSyncExternalStore } from "react";
import { connectToQuizHub } from "./signalr-hub";
import { createLivePhone, createLiveProjector, type LiveStore } from "./store";
import type { PhoneCommands, PhoneView, ProjectorCommands, ProjectorView } from "./types";

// The React side: a store per mounted screen and Masterclass, connected while the screen is mounted.

/** The projector (Host) of an Open Masterclass: what to show, plus startSession and resume. */
export function useLiveProjector(masterclassId: number, initialParticipants: number): ProjectorView & ProjectorCommands {
  const store = useStore(masterclassId, () => createLiveProjector(masterclassId, initialParticipants, connectToQuizHub));
  const view = useView(store);
  return { ...view, startSession: store.startSession, resume: store.resume };
}

/** A Participant's phone: what to show, plus answer. */
export function useLivePhone(masterclassId: number): PhoneView & PhoneCommands {
  const store = useStore(masterclassId, () => createLivePhone(masterclassId, connectToQuizHub));
  const view = useView(store);
  return { ...view, answer: store.answer };
}

// One store per Masterclass: a new one (fresh state, new connection) if the Masterclass changes. Later
// changes to the starting values (e.g. initialParticipants after a refresh) don't reset a running screen.
function useStore<Store extends LiveStore<unknown, object>>(masterclassId: number, create: () => Store): Store {
  const [held, setHeld] = useState(() => ({ masterclassId, store: create() }));
  let { store } = held;
  if (held.masterclassId !== masterclassId) {
    store = create();
    setHeld({ masterclassId, store });
  }
  useEffect(() => store.start(), [store]);
  return store;
}

function useView<View>(store: LiveStore<View, object>): View {
  // The first render on the server and in the browser shows the initial state (waiting, not connected).
  return useSyncExternalStore(store.subscribe, store.getSnapshot, store.getSnapshot);
}
