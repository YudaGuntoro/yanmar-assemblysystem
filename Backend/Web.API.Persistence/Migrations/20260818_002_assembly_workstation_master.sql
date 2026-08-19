CREATE TABLE IF NOT EXISTS assembly_workstations (
    id INT AUTO_INCREMENT PRIMARY KEY,
    workstation_code VARCHAR(50) NOT NULL,
    workstation_name VARCHAR(120) NOT NULL,
    workstation_no INT NOT NULL,
    description VARCHAR(255) NULL,
    is_deleted TINYINT(1) NOT NULL DEFAULT 0,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uq_assembly_workstations_code (workstation_code),
    KEY ix_assembly_workstations_no (workstation_no)
);

CREATE TABLE IF NOT EXISTS assembly_tools (
    id INT AUTO_INCREMENT PRIMARY KEY,
    workstation_id INT NOT NULL,
    tool_code VARCHAR(50) NOT NULL,
    tool_name VARCHAR(120) NOT NULL,
    nut_size VARCHAR(40) NOT NULL,
    program_no INT NULL,
    torque_standard DECIMAL(8, 2) NOT NULL DEFAULT 0,
    torque_min DECIMAL(8, 2) NOT NULL DEFAULT 0,
    torque_max DECIMAL(8, 2) NOT NULL DEFAULT 0,
    unit VARCHAR(20) NOT NULL DEFAULT 'N.m',
    sequence_no INT NOT NULL DEFAULT 0,
    is_deleted TINYINT(1) NOT NULL DEFAULT 0,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uq_assembly_tools_workstation_tool (workstation_id, tool_code),
    KEY ix_assembly_tools_nut_size (nut_size),
    KEY ix_assembly_tools_sequence_no (sequence_no),
    CONSTRAINT fk_assembly_tools_workstation
        FOREIGN KEY (workstation_id) REFERENCES assembly_workstations (id)
        ON UPDATE CASCADE
        ON DELETE CASCADE
);
