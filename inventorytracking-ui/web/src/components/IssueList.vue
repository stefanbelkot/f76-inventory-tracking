<script setup lang="ts">
import type { LoadIssue } from '../types/api'

defineProps<{ issues: LoadIssue[] }>()

function title(issue: LoadIssue): string {
  return issue.file || 'Folder'
}
</script>

<template>
  <section v-if="issues.length > 0" class="banner" aria-label="Problems while loading the export files">
    <strong>{{ issues.length === 1 ? '1 file could not be used' : `${issues.length} files could not be used` }}</strong>
    <ul>
      <li v-for="issue in issues" :key="`${issue.file}|${issue.message}`">
        <code>{{ title(issue) }}</code>: {{ issue.message }}
      </li>
    </ul>
  </section>
</template>

<style scoped>
ul {
  margin: 4px 0 0;
  padding-left: 20px;
}
</style>
