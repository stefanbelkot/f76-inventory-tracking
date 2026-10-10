<script setup lang="ts">
import { computed } from 'vue'
import { usePreferences } from '../composables/usePreferences'
import type { ThemeChoice } from '../types/api'

const NEXT: Record<ThemeChoice, ThemeChoice> = { system: 'light', light: 'dark', dark: 'system' }
const LABEL: Record<ThemeChoice, string> = { system: 'System theme', light: 'Light theme', dark: 'Dark theme' }

const { preferences, update } = usePreferences()
const theme = computed(() => preferences.value.theme)
</script>

<template>
  <button type="button" class="btn" :title="`${LABEL[theme]} (click to change)`" @click="update({ theme: NEXT[theme] })">
    {{ LABEL[theme] }}
  </button>
</template>
