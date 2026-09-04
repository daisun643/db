<template>
  <OrdersPanel
    :ref="setPanel"
    @pay="handlePay"
    @confirm="handleConfirm"
    @cancel="handleCancel"
    @dispute="openDispute"
    @messages="openOrderMessages"
    @error="error = $event"
  />
</template>

<script setup>
import OrdersPanel from '../../components/market/OrdersPanel.vue'
import { useMarket } from '../../composables/useMarket'

const { error, registerPanel, handlePay, handleConfirm, handleCancel, openDispute, openOrderMessages } = useMarket()

// 面板挂载时注册刷新句柄（离开页面时自动注销），供 useMarket 操作后统一刷新
let unregisterPanel = null
const setPanel = (el) => {
  unregisterPanel?.()
  unregisterPanel = el ? registerPanel({ reloadOrders: () => el.reload() }) : null
}
</script>
