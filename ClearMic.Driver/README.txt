ClearMic APO Driver
===================
Phase 3 — Audio Processing Object (COM C++)

Ce dossier contiendra l'APO Windows personnalisé qui créera
un périphérique micro virtuel natif, éliminant la dépendance
à VB-Cable.

Structure attendue :
  ClearMicAPO.cpp    — Implémentation COM de l'APO
  ClearMicAPO.h      — Interface
  ClearMicAPO.def    — Export definitions
  ClearMicAPO.inf    — Fichier d'installation driver
  Makefile           — Build avec MSVC / WDK

Référence : https://learn.microsoft.com/en-us/windows-hardware/drivers/audio/audio-processing-object-architecture
