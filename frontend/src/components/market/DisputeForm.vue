<template>
  <form v-if="open" class="dispute-form" @submit.prevent="$emit('submit')">
    <h2>订单 #{{ order?.transactionID }} 纠纷</h2>
    <textarea
      :value="reason"
      @input="$emit('update:reason', $event.target.value)"
      placeholder="描述纠纷原因"
      required
    ></textarea>
    <div class="form-row">
      <button class="btn btn-primary" type="submit">提交纠纷</button>
      <button class="btn" type="button" @click="$emit('close')">取消</button>
    </div>
  </form>
</template>

<script setup>
defineProps({
  open: { type: Boolean, default: false },
  order: { type: Object, default: null },
  reason: { type: String, default: '' },
})

defineEmits(['close', 'submit', 'update:reason'])
</script>

<style scoped>
.dispute-form {
  background: linear-gradient(180deg, color-mix(in oklab, var(--surface) 94%, #f8fafc 6%), var(--surface));
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 1rem;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.dispute-form h2 {
  font-size: 1rem;
}

.dispute-form textarea {
  border: 1px solid var(--border);
  border-radius: var(--radius);
  font: inherit;
  min-height: 96px;
  padding: 0.625rem 0.75rem;
  resize: vertical;
}

.form-row {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.form-row input {
  min-width: 120px;
}

@media (max-width: 760px) {
  .form-row {
    align-items: stretch;
    flex-direction: column;
  }
}
</style>

<style scoped>
/* Marketplace theme */
.dispute-form { border: 1px solid rgba(25,34,59,.08); border-radius: 22px; background: #fff; box-shadow: 0 12px 35px rgba(29,35,58,.05); }
.dispute-form textarea { border: 1px solid #e3e5ed; border-radius: 12px; background: #fff; transition: border-color .2s, box-shadow .2s; }
.dispute-form textarea:focus { border-color: #7463ee; outline: none; box-shadow: 0 0 0 4px rgba(105,87,245,.1); }
.dispute-form .form-row { align-items: stretch; }
.dispute-form .form-row > * { flex: 1 1 130px; }
</style>
