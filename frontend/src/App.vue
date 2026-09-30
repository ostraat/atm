<script setup lang="ts">
import { headerBackHandler } from './headerBack'
</script>

<template>
  <header class="app-header">
    <div class="app-header-side app-header-left">
      <button
        v-if="headerBackHandler"
        type="button"
        class="secondary header-back"
        @click="headerBackHandler()"
      >
        Back
      </button>
    </div>
    <h1 class="app-title">ATM</h1>
    <div class="app-header-side app-header-right" aria-hidden="true"></div>
  </header>
  <router-view />
</template>
