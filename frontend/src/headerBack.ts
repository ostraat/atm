import { ref, type Ref } from 'vue'

export type HeaderBackHandler = (() => void) | null

export const headerBackHandler: Ref<HeaderBackHandler> = ref(null)

export function setHeaderBack(handler: HeaderBackHandler): void {
  headerBackHandler.value = handler
}
