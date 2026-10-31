import { Form, Input, Select } from "antd";
import type { Zone, ZoneInput } from "@cafecr/api-client";
import { adminApi } from "../../api";
import { CrudPage } from "../../components/CrudPage";
import { useBranchOptions } from "../lookups";

export function ZonesPage() {
  const branches = useBranchOptions();

  return (
    <CrudPage<Zone, ZoneInput, ZoneInput, ZoneInput, Zone>
      title="Khu vực"
      queryKey="zones"
      resource={adminApi.zones}
      columns={[
        { title: "Tên", dataIndex: "name" },
        { title: "Chi nhánh", dataIndex: "branchId", render: (id: string) => branches.nameOf(id) },
      ]}
      initialValues={{}}
      toFormValues={({ id: _id, ...z }) => z}
      toCreate={(v) => v}
      toUpdate={(v) => v}
      renderFields={() => (
        <>
          <Form.Item label="Chi nhánh" name="branchId" rules={[{ required: true }]}>
            <Select options={branches.options} showSearch optionFilterProp="label" />
          </Form.Item>
          <Form.Item label="Tên" name="name" rules={[{ required: true, max: 100 }]}>
            <Input />
          </Form.Item>
        </>
      )}
    />
  );
}
