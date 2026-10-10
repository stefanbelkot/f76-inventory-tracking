<template>
  <main class="stopped">
    <h1>The viewer has stopped.</h1>
    <p class="muted">You can close this tab. Start the viewer again to see your data.</p>
  </main>
</template>

<style scoped>
.stopped {
  display: grid;
  place-content: center;
  height: 100%;
  text-align: center;
  gap: 8px;
  padding: 16px;
}
</style>
